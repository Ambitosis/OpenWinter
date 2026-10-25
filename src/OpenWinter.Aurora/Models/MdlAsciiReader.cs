using System.Globalization;
using System.Numerics;
using OpenWinter.Content;

namespace OpenWinter.Aurora.Models;

/// <summary>Reader for text (uncompiled) Aurora MDL models and walkmesh files (PWK/DWK/WOK text form).</summary>
public sealed partial class MdlAsciiReader
{
    static readonly HashSet<string> CountedLists = new(StringComparer.OrdinalIgnoreCase)
    {
        "verts", "faces", "tverts", "tverts1", "tverts2", "tverts3", "weights", "constraints", "colors",
        "normals", "tangents", "texindices0", "texindices1", "texindices2", "texindices3", "multimaterial",
        "flarecolorshifts", "flaresizes", "flarepositions", "texturenames",
    };

    readonly string[] _lines;
    int _i;

    MdlAsciiReader(string text) { _lines = text.Replace("\r", "").Split('\n'); }

    /// <summary>Reads an ASCII model; malformed data throws <see cref="InvalidDataException"/>.</summary>
    public static ModelData Read(byte[] buf)
    {
        try
        {
            return new MdlAsciiReader(Bin.Text.GetString(buf)).ReadModel();
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or OverflowException or KeyNotFoundException)
        {
            throw new InvalidDataException($"MDL: {e.Message}", e);
        }
    }

    static float P(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
    static int PI(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : (int)P(s);

    string[] NextTokens()
    {
        while (_i < _lines.Length)
        {
            var line = _lines[_i++];
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            var t = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length > 0) return t;
        }
        return Array.Empty<string>();
    }

    sealed class RawNode
    {
        public string Type = "", Name = "", Parent = "";
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.Identity;
        public readonly Dictionary<string, List<string[]>> Lists = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string[]> Props = new(StringComparer.OrdinalIgnoreCase);
    }

    ModelData ReadModel()
    {
        var m = new ModelData();
        var raw = new List<RawNode>();
        while (_i < _lines.Length)
        {
            var t = NextTokens();
            if (t.Length == 0) break;
            switch (t[0].ToLowerInvariant())
            {
                case "newmodel": m.Name = t.Length > 1 ? t[1] : ""; break;
                case "setsupermodel":
                    m.Supermodel = t.Length > 2 ? t[2] : "";
                    if (m.Supermodel.Equals("null", StringComparison.OrdinalIgnoreCase)) m.Supermodel = "";
                    break;
                case "setanimationscale": m.AnimationScale = t.Length > 1 ? P(t[1]) : 1f; break;
                case "classification":
                    m.Classification = (t.Length > 1 ? t[1].ToLowerInvariant() : "") switch
                    {
                        "effect" => ModelClassification.Effect,
                        "tile" => ModelClassification.Tile,
                        "character" => ModelClassification.Character,
                        "door" => ModelClassification.Door,
                        "item" => ModelClassification.Item,
                        "gui" => ModelClassification.Gui,
                        _ => ModelClassification.Other,
                    };
                    break;
                case "node": raw.Add(ReadNode(t)); break;
                case "newanim":
                    var anim = ReadAnimation(t);
                    m.Animations[anim.Name] = anim;
                    break;
                case "donemodel": goto done;
            }
        }
    done:
        if (m.AnimationScale <= 0) m.AnimationScale = 1f;
        Resolve(m, raw);
        return m;
    }

    RawNode ReadNode(string[] header)
    {
        var n = new RawNode { Type = header.Length > 1 ? header[1].ToLowerInvariant() : "", Name = header.Length > 2 ? header[2] : "" };
        while (_i < _lines.Length)
        {
            var t = NextTokens();
            if (t.Length == 0) break;
            var key = t[0];
            if (key.Equals("endnode", StringComparison.OrdinalIgnoreCase)) break;
            switch (key.ToLowerInvariant())
            {
                case "parent": n.Parent = t.Length > 1 ? t[1] : ""; break;
                case "position": n.Position = V3(t, 1); break;
                case "orientation": n.Rotation = AxisAngle(t, 1); break;
                case "aabb": SkipNumericLines(); break;
                default:
                    if (CountedLists.Contains(key) && t.Length > 1 && int.TryParse(t[1], out int rows))
                    {
                        // A row count past the end of the file reads the rows that are there.
                        var list = new List<string[]>(Math.Clamp(rows, 0, _lines.Length - _i));
                        for (int r = 0; r < rows && _i < _lines.Length; r++) list.Add(NextTokens());
                        n.Lists[key] = list;
                    }
                    else n.Props[key] = t;
                    break;
            }
        }
        return n;
    }

    void SkipNumericLines()
    {
        while (_i < _lines.Length)
        {
            var t = _lines[_i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length > 0 && !float.TryParse(t[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return;
            _i++;
        }
    }

    void Resolve(ModelData m, List<RawNode> raw)
    {
        var byName = new Dictionary<string, RawNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in raw) byName.TryAdd(n.Name, n);
        var ordered = new List<RawNode>();
        var placed = new HashSet<RawNode>();
        void Visit(RawNode n, int depth)
        {
            if (placed.Contains(n) || depth > 256) return;
            placed.Add(n);
            if (byName.TryGetValue(n.Parent, out var p) && p != n) Visit(p, depth + 1);
            ordered.Add(n);
        }
        foreach (var n in raw) Visit(n, 0);

        var indexOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in ordered)
        {
            var part = new ModelPart
            {
                Name = n.Name,
                Parent = indexOf.TryGetValue(n.Parent, out int pi) ? pi : -1,
                Position = n.Position,
                Rotation = n.Rotation,
            };
            indexOf.TryAdd(n.Name, m.Parts.Count);
            m.Parts.Add(part);
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            var n = ordered[i];
            var part = m.Parts[i];
            switch (n.Type)
            {
                case "trimesh" or "skin" or "danglymesh" or "animmesh" or "aabb":
                    if (n.Lists.ContainsKey("faces"))
                    {
                        part.Mesh = BuildMesh(n, indexOf);
                        part.Mesh.IsWalkmesh = n.Type == "aabb";
                        part.Mesh.IsDangly = n.Type == "danglymesh";
                        part.TileFadeMode = Prop(n, "tilefade") is { Length: > 1 } tf ? PI(tf[1]) : 0;
                    }
                    break;
                case "light":
                    part.Light = new LightData
                    {
                        Color = Prop(n, "color") is { Length: > 3 } c ? V3(c, 1) : Vector3.One,
                        Radius = Prop(n, "radius") is { Length: > 1 } r ? P(r[1]) : 5f,
                        Multiplier = Prop(n, "multiplier") is { Length: > 1 } mu ? P(mu[1]) : 1f,
                        IsDynamic = Prop(n, "isdynamic") is { Length: > 1 } d && PI(d[1]) != 0,
                        AffectsDynamic = Prop(n, "affectdynamic") is { Length: > 1 } ad && PI(ad[1]) != 0,
                        Shadow = Prop(n, "shadow") is { Length: > 1 } sh && PI(sh[1]) != 0,
                        Priority = Prop(n, "lightpriority") is { Length: > 1 } lp ? PI(lp[1]) : 0,
                    };
                    break;
                case "emitter":
                    part.Emitter = BuildEmitter(n);
                    break;
                case "reference":
                    string reference = Prop(n, "refmodel") is { Length: > 1 } rm ? rm[1] : "";
                    part.Reference = new ModelReference
                    {
                        Model = reference.Equals("null", StringComparison.OrdinalIgnoreCase) ? "" : reference,
                        Reattachable = Prop(n, "reattachable") is { Length: > 1 } ra && PI(ra[1]) != 0,
                    };
                    break;
            }
        }
    }

    static string[]? Prop(RawNode n, string key) => n.Props.TryGetValue(key, out var v) ? v : null;

    static EmitterData BuildEmitter(RawNode n)
    {
        var e = new EmitterData { AlphaStart = 1, AlphaEnd = 1, Velocity = 1 };
        string Str(string key, string fallback) => Prop(n, key) is { Length: > 1 } v ? v[1] : fallback;
        bool Flag(params string[] keys)
        {
            foreach (var key in keys)
                if (Prop(n, key) is { Length: > 1 } v) return v[1].Equals("true", StringComparison.OrdinalIgnoreCase) || PI(v[1]) != 0;
            return false;
        }
        int Sel(string key) => Prop(n, key) is { Length: > 1 } v ? PI(v[1]) : 0;
        e.Texture = Str("texture", "");
        if (e.Texture.Equals("null", StringComparison.OrdinalIgnoreCase)) e.Texture = "";
        // Names win; the modelling tool's numeric selections (update_sel ...) stand in when a name is missing.
        e.Update = Str("update", Sel("update_sel") switch { 2 => "Explosion", 3 => "Single", 4 => "Lightning", _ => "Fountain" });
        e.Render = Str("render", Sel("render_sel") switch
        {
            2 => "Linked",
            3 => "Billboard_to_Local_Z",
            4 => "Billboard_to_World_Z",
            5 => "Aligned_to_World_Z",
            6 => "Aligned_to_Particle_Dir",
            7 => "Motion_Blur",
            _ => "Normal",
        });
        e.Blend = Str("blend", Sel("blend_sel") switch { 2 => "Punch-Through", 3 => "Lighten", _ => "Normal" });
        e.Chunk = Str("chunkName", "");
        if (e.Chunk.Equals("null", StringComparison.OrdinalIgnoreCase)) e.Chunk = "";
        e.SpawnType = Prop(n, "spawntype") is { Length: > 1 } st ? (st[1].Equals("Trail", StringComparison.OrdinalIgnoreCase) ? 1 : Math.Max(0, PI(st[1]))) : 0;
        if (e.SpawnType > 1) e.SpawnType = 0;
        e.RenderOrder = Sel("renderorder");
        e.TwoSided = Flag("twosidedtex");
        e.Loop = Flag("loop");
        e.P2P = Flag("p2p");
        e.P2PBezier = Prop(n, "p2p_type") is { Length: > 1 } pt ? !pt[1].Equals("Gravity", StringComparison.OrdinalIgnoreCase) : Sel("p2p_sel") != 2;
        // Authored native controls: explicit 0/2 share the non-Bezier path, 1
        // selects Bezier, including a contradictory p2p_type name. Preserve
        // the existing fallback for absent or unmeasured selector values.
        if (Prop(n, "p2p_sel") is { Length: > 1 } ps &&
            int.TryParse(ps[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int selector) &&
            selector is >= 0 and <= 2)
            e.P2PBezier = selector == 1;
        e.AffectedByWind = Flag("affectedByWind");
        e.Tinted = Flag("m_isTinted", "m_istnited");
        e.Bounce = Flag("bounce");
        e.Random = Flag("random");
        e.Inherit = Flag("inherit");
        e.InheritVel = Flag("inheritvel");
        e.InheritLocal = Flag("inherit_local");
        e.InheritPart = Flag("inherit_part");
        e.Splat = Flag("splat");
        e.DeadSpace = Prop(n, "deadspace") is { Length: > 1 } ds ? P(ds[1]) : 0;
        e.BlastRadius = Prop(n, "blastRadius") is { Length: > 1 } br ? P(br[1]) : 0;
        e.BlastLength = Prop(n, "blastLength") is { Length: > 1 } bl ? P(bl[1]) : 0;
        e.GridX = Math.Clamp(Sel("xgrid"), 1, 64);
        e.GridY = Math.Clamp(Sel("ygrid"), 1, 64);
        foreach (var (key, param) in EmitterParamNames)
        {
            if (Prop(n, key) is not { Length: > 1 } v) continue;
            if (EmitterData.IsColor(param))
            {
                if (v.Length > 3) for (int c = 0; c < 3; c++) e.Set(param, P(v[1 + c]), c);
            }
            else e.Set(param, P(v[1]));
        }
        return e;
    }

    /// <summary>Text meshes index positions and UVs separately per corner; split into one vertex per unique pair.</summary>
    static MeshData BuildMesh(RawNode n, Dictionary<string, int> indexOf)
    {
        var m = new MeshData();
        var bitmap = Prop(n, "bitmap") is { Length: > 1 } b ? b[1] : "";
        m.Texture = bitmap.Equals("null", StringComparison.OrdinalIgnoreCase) ? "" : bitmap;
        m.Render = Prop(n, "render") is not { Length: > 1 } r || PI(r[1]) != 0;
        m.TransparencyHint = Prop(n, "transparencyhint") is { Length: > 1 } th ? PI(th[1]) : 0;
        m.Diffuse = Prop(n, "diffuse") is { Length: > 3 } d ? V3(d, 1) : Vector3.One;
        m.Ambient = Prop(n, "ambient") is { Length: > 3 } amb ? V3(amb, 1) : Vector3.One;
        m.Specular = Prop(n, "specular") is { Length: > 3 } sp ? V3(sp, 1) : Vector3.Zero;
        m.SelfIllumination = Prop(n, "selfillumcolor") is { Length: > 3 } si ? V3(si, 1) : Vector3.Zero;
        m.Alpha = Prop(n, "alpha") is { Length: > 1 } al ? P(al[1]) : 1f;
        m.Beaming = Prop(n, "beaming") is { Length: > 1 } be && PI(be[1]) != 0;
        m.Shadow = Prop(n, "shadow") is not { Length: > 1 } s || PI(s[1]) != 0;

        var positions = n.Lists.TryGetValue("verts", out var vl) ? vl.Select(v => V3(v, 0)).ToArray() : Array.Empty<Vector3>();
        var tverts = n.Lists.TryGetValue("tverts", out var tl) ? tl.Select(v => v.Length > 1 ? new Vector2(P(v[0]), 1f - P(v[1])) : Vector2.Zero).ToArray() : Array.Empty<Vector2>();
        var faces = n.Lists["faces"];

        var smooth = new Vector3[positions.Length];
        foreach (var f in faces)
        {
            if (f.Length < 3) continue;
            int a = PI(f[0]), b2 = PI(f[1]), c = PI(f[2]);
            if (a >= positions.Length || b2 >= positions.Length || c >= positions.Length || a < 0 || b2 < 0 || c < 0) continue;
            var fn = Vector3.Cross(positions[b2] - positions[a], positions[c] - positions[a]);
            smooth[a] += fn; smooth[b2] += fn; smooth[c] += fn;
        }

        int[][]? skinBones = null;
        float[][]? skinWeights = null;
        if (n.Type == "skin" && n.Lists.TryGetValue("weights", out var wl) && wl.Count == positions.Length)
        {
            skinBones = new int[wl.Count][];
            skinWeights = new float[wl.Count][];
            for (int i = 0; i < wl.Count; i++)
            {
                var bones = new int[] { -1, -1, -1, -1 };
                var weights = new float[4];
                for (int k = 0, slot = 0; k + 1 < wl[i].Length && slot < 4; k += 2, slot++)
                {
                    bones[slot] = indexOf.TryGetValue(wl[i][k], out int bi) ? bi : -1;
                    weights[slot] = P(wl[i][k + 1]);
                }
                skinBones[i] = bones;
                skinWeights[i] = weights;
            }
        }

        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        var surfaces = new List<int>();
        var boneParts = new List<int>();
        var boneWeights = new List<float>();
        var remap = new Dictionary<long, int>();
        foreach (var f in faces)
        {
            if (f.Length < 3) continue;
            int p0 = PI(f[0]), p1 = PI(f[1]), p2 = PI(f[2]);
            if (p0 < 0 || p1 < 0 || p2 < 0 || p0 >= positions.Length || p1 >= positions.Length || p2 >= positions.Length) continue;
            int t0 = f.Length > 4 ? PI(f[4]) : 0, t1 = f.Length > 5 ? PI(f[5]) : 0, t2 = f.Length > 6 ? PI(f[6]) : 0;
            surfaces.Add(f.Length > 7 ? PI(f[7]) : 0);
            // Reverse winding (Aurora CCW → renderer CW).
            foreach (var (p, t) in new[] { (p0, t0), (p2, t2), (p1, t1) })
            {
                long key = ((long)p << 32) | (uint)t;
                if (!remap.TryGetValue(key, out int vi))
                {
                    vi = verts.Count;
                    remap[key] = vi;
                    verts.Add(positions[p]);
                    normals.Add(smooth[p].LengthSquared() > 0 ? Vector3.Normalize(smooth[p]) : Vector3.UnitZ);
                    uvs.Add(t >= 0 && t < tverts.Length ? tverts[t] : Vector2.Zero);
                    if (skinBones != null)
                    {
                        boneParts.AddRange(skinBones[p]);
                        boneWeights.AddRange(skinWeights![p]);
                    }
                }
                indices.Add(vi);
            }
        }
        m.Vertices = verts.ToArray();
        m.Normals = normals.ToArray();
        m.Uvs = uvs.ToArray();
        m.Indices = indices.ToArray();
        m.FaceSurfaces = surfaces.ToArray();
        if (skinBones != null)
        {
            m.BoneParts = boneParts.ToArray();
            m.BoneWeights = boneWeights.ToArray();
        }
        return m;
    }

    ModelAnimation ReadAnimation(string[] header)
    {
        var anim = new ModelAnimation { Name = header.Length > 1 ? header[1] : "" };
        while (_i < _lines.Length)
        {
            var t = NextTokens();
            if (t.Length == 0) break;
            switch (t[0].ToLowerInvariant())
            {
                case "doneanim": return anim;
                case "length": anim.Length = P(t[1]); break;
                case "transtime": anim.Transition = P(t[1]); break;
                case "animroot": anim.Root = t.Length > 1 ? t[1] : ""; break;
                case "event": anim.Events.Add((P(t[1]), t.Length > 2 ? t[2] : "")); break;
                case "node":
                    var track = ReadAnimNode(t.Length > 1 ? t[1].ToLowerInvariant() : "");
                    if (track != null && t.Length > 2) anim.Tracks[t[2]] = track;
                    break;
            }
        }
        return anim;
    }

    AnimationTrack? ReadAnimNode(string type)
    {
        var pt = new List<float>(); var pv = new List<Vector3>();
        var rt = new List<float>(); var rv = new List<Quaternion>();
        var track = new AnimationTrack();
        bool any = false;
        while (_i < _lines.Length)
        {
            var t = NextTokens();
            if (t.Length == 0) break;
            var key = t[0].ToLowerInvariant();
            if (key == "endnode") break;
            if (key == "scale" && t.Length > 1)
            {
                track.ScaleTimes = [0f];
                track.Scales = [P(t[1])];
                any = true;
            }
            else if (type == "light" && key is "radius" or "multiplier" or "color")
            {
                if (key == "color" && t.Length > 3) { track.LightColorTimes = [0f]; track.LightColors = [V3(t, 1)]; any = true; }
                else if (key == "radius" && t.Length > 1) { track.LightRadiusTimes = [0f]; track.LightRadii = [P(t[1])]; any = true; }
                else if (key == "multiplier" && t.Length > 1) { track.LightTimes = [0f]; track.LightMultipliers = [P(t[1])]; any = true; }
            }
            else if (key is "positionkey" or "orientationkey")
            {
                foreach (var r in KeyRows(t))
                {
                    if (key == "positionkey" && r.Length >= 4) { pt.Add(P(r[0])); pv.Add(V3(r, 1)); }
                    else if (key == "orientationkey" && r.Length >= 5) { rt.Add(P(r[0])); rv.Add(AxisAngle(r, 1)); }
                }
            }
            else if (key.EndsWith("key"))
            {
                var rows = KeyRows(t);
                var name = key[..^3];
                if (name == "scale")
                {
                    var ok = rows.Where(r => r.Length > 1).ToList();
                    track.ScaleTimes = ok.Select(r => P(r[0])).ToArray();
                    track.Scales = ok.Select(r => P(r[1])).ToArray();
                    any |= ok.Count > 0;
                }
                else if (type == "emitter" && EmitterParamNames.TryGetValue(name, out var param))
                {
                    int width = EmitterData.IsColor(param) ? 3 : 1;
                    var ok = rows.Where(r => r.Length > width).ToList();
                    if (ok.Count == 0) continue;
                    track.EmitterKeys[param] = new KeyTrack
                    {
                        Times = ok.Select(r => P(r[0])).ToArray(),
                        Values = ok.SelectMany(r => r.Skip(1).Take(width).Select(P)).ToArray(),
                        Width = width,
                    };
                    any = true;
                }
                else if (type == "light" && name is "radius" or "multiplier" or "color")
                {
                    int width = name == "color" ? 3 : 1;
                    var ok = rows.Where(r => r.Length > width).ToList();
                    var times = ok.Select(r => P(r[0])).ToArray();
                    if (name == "radius") { track.LightRadiusTimes = times; track.LightRadii = ok.Select(r => P(r[1])).ToArray(); }
                    else if (name == "multiplier") { track.LightTimes = times; track.LightMultipliers = ok.Select(r => P(r[1])).ToArray(); }
                    else { track.LightColorTimes = times; track.LightColors = ok.Select(r => V3(r, 1)).ToArray(); }
                    any |= ok.Count > 0;
                }
                else if (name == "alpha" && type != "emitter" && type != "light")
                {
                    var ok = rows.Where(r => r.Length > 1).ToList();
                    track.AlphaTimes = ok.Select(r => P(r[0])).ToArray();
                    track.Alphas = ok.Select(r => P(r[1])).ToArray();
                    any |= ok.Count > 0;
                }
            }
        }
        if (pt.Count == 0 && rt.Count == 0 && !any) return null;
        track.PositionTimes = pt.ToArray();
        track.Positions = pv.ToArray();
        track.RotationTimes = rt.ToArray();
        track.Rotations = rv.ToArray();
        if (track.EmitterKeys.TryGetValue(EmitterParam.BirthRate, out var birth))
        {
            track.BirthRateTimes = birth.Times;
            track.BirthRates = birth.Values;
        }
        return track;
    }

    /// <summary>Text names of the emitter controllers (case-insensitive).</summary>
    internal static readonly Dictionary<string, EmitterParam> EmitterParamNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alphaStart"] = EmitterParam.AlphaStart, ["alphaEnd"] = EmitterParam.AlphaEnd, ["alphaMid"] = EmitterParam.AlphaMid,
        ["birthrate"] = EmitterParam.BirthRate, ["bounce_co"] = EmitterParam.BounceCo, ["colorStart"] = EmitterParam.ColorStart,
        ["colorEnd"] = EmitterParam.ColorEnd, ["colorMid"] = EmitterParam.ColorMid, ["combinetime"] = EmitterParam.CombineTime,
        ["drag"] = EmitterParam.Drag, ["fps"] = EmitterParam.Fps, ["frameEnd"] = EmitterParam.FrameEnd, ["frameStart"] = EmitterParam.FrameStart,
        ["grav"] = EmitterParam.Grav, ["lifeExp"] = EmitterParam.LifeExp, ["mass"] = EmitterParam.Mass,
        ["p2p_bezier2"] = EmitterParam.P2PBezier2, ["p2p_bezier3"] = EmitterParam.P2PBezier3, ["particleRot"] = EmitterParam.ParticleRot,
        ["randvel"] = EmitterParam.RandVel, ["sizeStart"] = EmitterParam.SizeStart, ["sizeEnd"] = EmitterParam.SizeEnd,
        ["sizeStart_y"] = EmitterParam.SizeStartY, ["sizeEnd_y"] = EmitterParam.SizeEndY, ["sizeMid"] = EmitterParam.SizeMid,
        ["sizeMid_y"] = EmitterParam.SizeMidY, ["spread"] = EmitterParam.Spread, ["threshold"] = EmitterParam.Threshold,
        ["velocity"] = EmitterParam.Velocity, ["xsize"] = EmitterParam.XSize, ["ysize"] = EmitterParam.YSize,
        ["blurlength"] = EmitterParam.BlurLength, ["lightningDelay"] = EmitterParam.LightningDelay,
        ["lightningRadius"] = EmitterParam.LightningRadius, ["lightningScale"] = EmitterParam.LightningScale,
        ["lightningSubDiv"] = EmitterParam.LightningSubDiv, ["lightningzigzag"] = EmitterParam.LightningZigZag,
        ["detonate"] = EmitterParam.Detonate, ["percentStart"] = EmitterParam.PercentStart, ["percentMid"] = EmitterParam.PercentMid,
        ["percentEnd"] = EmitterParam.PercentEnd,
    };

    /// <summary>Keyed lists either give a row count or run until "endlist".</summary>
    List<string[]> KeyRows(string[] header)
    {
        var rows = new List<string[]>();
        if (header.Length > 1 && int.TryParse(header[1], out int n))
        {
            for (int r = 0; r < n && _i < _lines.Length; r++) rows.Add(NextTokens());
            return rows;
        }
        while (_i < _lines.Length)
        {
            var t = NextTokens();
            if (t.Length == 0 || t[0].Equals("endlist", StringComparison.OrdinalIgnoreCase)) break;
            rows.Add(t);
        }
        return rows;
    }

    static Vector3 V3(string[] t, int at) => t.Length < at + 3 ? Vector3.Zero : new Vector3(P(t[at]), P(t[at + 1]), P(t[at + 2]));

    static Quaternion AxisAngle(string[] t, int at)
    {
        if (t.Length < at + 4) return Quaternion.Identity;
        var axis = V3(t, at);
        float angle = P(t[at + 3]);
        if (axis.LengthSquared() < 1e-12f || MathF.Abs(angle) < 1e-9f) return Quaternion.Identity;
        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), angle);
    }
}
