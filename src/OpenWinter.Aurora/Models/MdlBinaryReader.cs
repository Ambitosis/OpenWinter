using System.Buffers.Binary;
using System.Numerics;
using OpenWinter.Content;

namespace OpenWinter.Aurora.Models;

/// <summary>
/// Reader for compiled (binary) Aurora MDL models.
/// Layout: a 12-byte file header (0, raw offset, raw size), the model data
/// block whose internal pointers are relative to byte 12, then a raw block
/// (vertex arrays) addressed relative to its own start.
/// </summary>
public sealed partial class MdlBinaryReader
{
    const uint NodeLight = 0x02, NodeEmitter = 0x04, NodeReference = 0x10, NodeMesh = 0x20, NodeSkin = 0x40, NodeDangly = 0x100, NodeAabb = 0x200;
    const uint None = 0xFFFFFFFF;

    // Controller ids (shared by node kinds where noted).
    const int CtrlPosition = 8, CtrlOrientation = 20, CtrlScale = 36;
    const int CtrlLightColor = 76, CtrlLightRadius = 88, CtrlLightMultiplier = 140;
    // Mesh alpha: animations key it as 128 (every base-game model); 132 kept for static values written that way.
    const int CtrlMeshAlpha = 128, CtrlMeshAlphaAlt = 132;
    const int CtrlMeshSelfIllumination = 100;
    const int CtrlEmBirthrate = 88;

    readonly byte[] _b;
    readonly int _raw;

    MdlBinaryReader(byte[] b)
    {
        _b = b;
        _raw = 12 + (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4));
    }

    public static bool IsBinary(byte[] buf) => buf.Length > 12 && BinaryPrimitives.ReadUInt32LittleEndian(buf) == 0;

    public static ModelData Read(byte[] buf) => new MdlBinaryReader(buf).ReadModel();

    uint U(int o) => BinaryPrimitives.ReadUInt32LittleEndian(_b.AsSpan(12 + o));
    float F(int o) => BinaryPrimitives.ReadSingleLittleEndian(_b.AsSpan(12 + o));
    string S(int o, int size) => Bin.FixedString(_b, 12 + o, size);
    float RawF(int o) => BinaryPrimitives.ReadSingleLittleEndian(_b.AsSpan(_raw + o));
    bool InRaw(long o, int size) => o >= 0 && _raw + o + size <= _b.Length;

    ModelData ReadModel()
    {
        var m = new ModelData
        {
            Name = S(0x08, 64),
            AnimationScale = F(0xA4),
            Supermodel = S(0xA8, 64),
            Classification = _b[12 + 0x72] switch
            {
                0x01 => ModelClassification.Effect,
                0x02 => ModelClassification.Tile,
                0x04 => ModelClassification.Character,
                0x08 => ModelClassification.Door,
                _ => ModelClassification.Other,
            },
        };
        if (m.Supermodel.Equals("null", StringComparison.OrdinalIgnoreCase)) m.Supermodel = "";
        if (m.AnimationScale <= 0 || float.IsNaN(m.AnimationScale)) m.AnimationScale = 1f;
        ReadNode((int)U(0x48), -1, m, 0);

        int animPtr = (int)U(0x78);
        int animCount = (int)U(0x7C);
        for (int i = 0; i < animCount; i++)
        {
            var anim = ReadAnimation((int)U(animPtr + i * 4));
            m.Animations[anim.Name] = anim;
        }
        return m;
    }

    /// <summary>
    /// Nodes are appended depth-first, pre-order: the same order skin bone
    /// tables use to refer to nodes.
    /// </summary>
    void ReadNode(int o, int parent, ModelData m, int depth)
    {
        if (depth > 128 || o <= 0 || 12 + o + 0x70 > _b.Length) return;
        var part = new ModelPart { Name = S(o + 0x20, 32), Parent = parent };
        uint flags = U(o + 0x6C);
        var keys = Controllers(o);
        if (keys.TryGetValue(CtrlPosition, out var pos)) part.Position = V3(pos.Values, 0);
        if (keys.TryGetValue(CtrlOrientation, out var rot) && rot.Width >= 4) part.Rotation = Q(rot.Values, 0);

        if ((flags & NodeMesh) != 0)
        {
            part.Mesh = ReadMesh(o, (flags & NodeSkin) != 0, (flags & NodeAabb) != 0);
            part.Mesh.IsWalkmesh = (flags & NodeAabb) != 0;
            part.Mesh.IsDangly = (flags & NodeDangly) != 0;
            part.TileFadeMode = (int)U(o + 0x1E8);
            if (keys.TryGetValue(CtrlMeshAlpha, out var alpha) || keys.TryGetValue(CtrlMeshAlphaAlt, out alpha)) part.Mesh.Alpha = alpha.Values[0];
            if (keys.TryGetValue(CtrlMeshSelfIllumination, out var illumination) && illumination.Width >= 3)
                part.Mesh.SelfIllumination = V3(illumination.Values, 0);
        }
        else if ((flags & NodeLight) != 0)
        {
            part.Light = new LightData
            {
                Priority = (int)U(o + 0xB0),
                IsDynamic = U(o + 0xB8) != 0,
                AffectsDynamic = U(o + 0xBC) != 0,
                Shadow = U(o + 0xC0) != 0,
            };
            if (keys.TryGetValue(CtrlLightColor, out var c) && c.Width >= 3) part.Light.Color = V3(c.Values, 0);
            if (keys.TryGetValue(CtrlLightRadius, out var r)) part.Light.Radius = r.Values[0];
            if (keys.TryGetValue(CtrlLightMultiplier, out var mul)) part.Light.Multiplier = mul.Values[0];
        }
        else if ((flags & NodeEmitter) != 0)
        {
            part.Emitter = ReadEmitter(o, keys);
        }
        else if ((flags & NodeReference) != 0 && 12L + o + 0xB4 <= _b.Length)
        {
            // Torlak's public Reference Node layout: CHAR[64] at 0x70, UINT32 at 0xB0.
            string reference = S(o + 0x70, 64);
            part.Reference = new ModelReference
            {
                Model = reference.Equals("null", StringComparison.OrdinalIgnoreCase) ? "" : reference,
                Reattachable = U(o + 0xB0) != 0,
            };
        }

        int index = m.Parts.Count;
        m.Parts.Add(part);
        int children = (int)U(o + 0x48);
        int count = (int)U(o + 0x4C);
        for (int i = 0; i < count; i++) ReadNode((int)U(children + i * 4), index, m, depth + 1);
    }

    sealed class Keys
    {
        public float[] Times = Array.Empty<float>();
        public float[] Values = Array.Empty<float>();
        public int Width;
    }

    /// <summary>Controllers by type; bezier keys are reduced to their value columns.</summary>
    Dictionary<int, Keys> Controllers(int o)
    {
        var result = new Dictionary<int, Keys>();
        int keyPtr = (int)U(o + 0x54);
        int keyCount = (int)U(o + 0x58);
        int dataPtr = (int)U(o + 0x60);
        int dataCount = (int)U(o + 0x64);
        for (int i = 0; i < keyCount; i++)
        {
            int k = 12 + keyPtr + i * 12;
            if (k + 12 > _b.Length) break;
            int type = (int)BinaryPrimitives.ReadUInt32LittleEndian(_b.AsSpan(k));
            int rows = BinaryPrimitives.ReadInt16LittleEndian(_b.AsSpan(k + 4));
            int timeIndex = BinaryPrimitives.ReadInt16LittleEndian(_b.AsSpan(k + 6));
            int dataIndex = BinaryPrimitives.ReadInt16LittleEndian(_b.AsSpan(k + 8));
            int columns = _b[k + 10];
            if (rows <= 0) continue;
            int width = columns & 0x0F;
            int stride = (columns & 0x10) != 0 ? width * 3 : width;
            if (width == 0 || timeIndex + rows > dataCount || dataIndex + (rows - 1) * stride + width > dataCount) continue;
            var keys = new Keys { Times = new float[rows], Values = new float[rows * width], Width = width };
            for (int r = 0; r < rows; r++)
            {
                keys.Times[r] = F(dataPtr + (timeIndex + r) * 4);
                for (int c = 0; c < width; c++)
                    keys.Values[r * width + c] = F(dataPtr + (dataIndex + r * stride + c) * 4);
            }
            result[type] = keys;
        }
        return result;
    }

    static Vector3 V3(float[] v, int row) => new(v[row * 3], v[row * 3 + 1], v[row * 3 + 2]);

    static Quaternion Q(float[] v, int row)
    {
        var q = new Quaternion(v[row * 4], v[row * 4 + 1], v[row * 4 + 2], v[row * 4 + 3]);
        return q.LengthSquared() > 1e-8f ? Quaternion.Normalize(q) : Quaternion.Identity;
    }

    MeshData ReadMesh(int o, bool skin, bool walkmesh)
    {
        var m = new MeshData
        {
            Diffuse = new Vector3(F(o + 0xAC), F(o + 0xB0), F(o + 0xB4)),
            Ambient = new Vector3(F(o + 0xB8), F(o + 0xBC), F(o + 0xC0)),
            Specular = new Vector3(F(o + 0xC4), F(o + 0xC8), F(o + 0xCC)),
            Shadow = U(o + 0xD4) != 0,
            Beaming = U(o + 0xD8) != 0,
            Render = U(o + 0xDC) != 0,
            TransparencyHint = (int)U(o + 0xE0),
            Texture = S(o + 0xE8, 64),
        };
        if (m.Texture.Equals("null", StringComparison.OrdinalIgnoreCase)) m.Texture = "";

        int count = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(12 + o + 0x230));
        uint verts = U(o + 0x22C), uvs = U(o + 0x234), normals = U(o + 0x244);
        if (verts == None || count == 0 || !InRaw(verts, count * 12)) return m;
        m.Vertices = new Vector3[count];
        for (int i = 0; i < count; i++)
            m.Vertices[i] = new Vector3(RawF((int)verts + i * 12), RawF((int)verts + i * 12 + 4), RawF((int)verts + i * 12 + 8));
        if (uvs != None && InRaw(uvs, count * 8))
        {
            m.Uvs = new Vector2[count];
            for (int i = 0; i < count; i++)
                m.Uvs[i] = new Vector2(RawF((int)uvs + i * 8), 1f - RawF((int)uvs + i * 8 + 4));
        }
        if (normals != None && InRaw(normals, count * 12))
        {
            m.Normals = new Vector3[count];
            for (int i = 0; i < count; i++)
                m.Normals[i] = new Vector3(RawF((int)normals + i * 12), RawF((int)normals + i * 12 + 4), RawF((int)normals + i * 12 + 8));
        }

        // Faces (32 bytes): plane normal + distance, surface id, 3 adjacent faces,
        // 3 vertex indices. Aurora winds front faces counter-clockwise, the
        // renderer clockwise, so swap the last two indices.
        int faces = (int)U(o + 0x78);
        int faceCount = (int)U(o + 0x7C);
        var idx = new List<int>(faceCount * 3);
        var surf = new List<int>(faceCount);
        for (int i = 0; i < faceCount; i++)
        {
            int f = 12 + faces + i * 32;
            if (f + 32 > _b.Length) break;
            int a = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(f + 26));
            int b = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(f + 28));
            int c = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(f + 30));
            if (a >= count || b >= count || c >= count) continue;
            idx.Add(a); idx.Add(c); idx.Add(b);
            surf.Add((int)BinaryPrimitives.ReadUInt32LittleEndian(_b.AsSpan(f + 16)));
        }
        m.Indices = idx.ToArray();
        m.FaceSurfaces = surf.ToArray();
        // The vertex index lists (counts at 0x204, raw-block offsets at 0x210) are what gets drawn.
        // Usually they repeat the face array, but meshes the toolset merged for rendering keep only
        // part of their triangles as faces (the rest exist only in the index list). Walkmeshes keep
        // their faces so each triangle still lines up with its surface id.
        if (!walkmesh && IndexLists(o, count) is { } drawn && drawn.Count > idx.Count) m.Indices = drawn.ToArray();

        if (skin)
        {
            uint weights = U(o + 0x27C), refs = U(o + 0x280);
            if (weights != None && refs != None && InRaw(weights, count * 16) && InRaw(refs, count * 8))
            {
                // Per-vertex refs index a 17-entry table of node indices.
                var boneNodes = new int[17];
                for (int b = 0; b < 17; b++) boneNodes[b] = BinaryPrimitives.ReadInt16LittleEndian(_b.AsSpan(12 + o + 0x2B0 + b * 2));
                m.BoneParts = new int[count * 4];
                m.BoneWeights = new float[count * 4];
                for (int i = 0; i < count; i++)
                    for (int k = 0; k < 4; k++)
                    {
                        int bone = BinaryPrimitives.ReadInt16LittleEndian(_b.AsSpan(_raw + (int)refs + i * 8 + k * 2));
                        float w = RawF((int)weights + i * 16 + k * 4);
                        int node = bone >= 0 && bone < 17 ? boneNodes[bone] : -1;
                        m.BoneParts[i * 4 + k] = node;
                        m.BoneWeights[i * 4 + k] = node >= 0 ? w : 0f;
                    }
            }
        }
        return m;
    }

    /// <summary>
    /// The mesh's triangle-list index arrays (u16 in the raw block), winding swapped like the faces;
    /// null when there are none or they don't fit the vertex count.
    /// </summary>
    List<int>? IndexLists(int o, int vertexCount)
    {
        uint countsPtr = U(o + 0x204), lists = U(o + 0x208), offsetsPtr = U(o + 0x210), offsets = U(o + 0x214);
        if (lists == 0 || lists > 64 || offsets < lists || countsPtr == None || offsetsPtr == None) return null;
        if (12 + (long)countsPtr + lists * 4 > _b.Length || 12 + (long)offsetsPtr + lists * 4 > _b.Length) return null;
        var result = new List<int>();
        for (int l = 0; l < lists; l++)
        {
            uint n = U((int)countsPtr + l * 4), at = U((int)offsetsPtr + l * 4);
            if (n == 0) continue;
            if (n % 3 != 0 || n > 3 * 65536 || at == None || !InRaw(at, (int)n * 2)) return null;
            for (int i = 0; i < n; i += 3)
            {
                int a = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(_raw + (int)at + i * 2));
                int b = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(_raw + (int)at + i * 2 + 2));
                int c = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(_raw + (int)at + i * 2 + 4));
                if (a >= vertexCount || b >= vertexCount || c >= vertexCount) return null;
                result.Add(a); result.Add(c); result.Add(b);
            }
        }
        return result.Count > 0 ? result : null;
    }

    /// <summary>Emitter controller ids (public community format descriptions; 220-228 unconfirmed).</summary>
    internal static readonly Dictionary<int, EmitterParam> EmitterParams = new()
    {
        [80] = EmitterParam.AlphaEnd, [84] = EmitterParam.AlphaStart, [88] = EmitterParam.BirthRate, [92] = EmitterParam.BounceCo,
        [96] = EmitterParam.ColorEnd, [108] = EmitterParam.ColorStart, [120] = EmitterParam.CombineTime, [124] = EmitterParam.Drag,
        [128] = EmitterParam.Fps, [132] = EmitterParam.FrameEnd, [136] = EmitterParam.FrameStart, [140] = EmitterParam.Grav,
        [144] = EmitterParam.LifeExp, [148] = EmitterParam.Mass, [152] = EmitterParam.P2PBezier2, [156] = EmitterParam.P2PBezier3,
        [160] = EmitterParam.ParticleRot, [164] = EmitterParam.RandVel, [168] = EmitterParam.SizeStart, [172] = EmitterParam.SizeEnd,
        [176] = EmitterParam.SizeStartY, [180] = EmitterParam.SizeEndY, [184] = EmitterParam.Spread, [188] = EmitterParam.Threshold,
        [192] = EmitterParam.Velocity, [196] = EmitterParam.XSize, [200] = EmitterParam.YSize, [204] = EmitterParam.BlurLength,
        [208] = EmitterParam.LightningDelay, [212] = EmitterParam.LightningRadius, [216] = EmitterParam.LightningScale,
        [220] = EmitterParam.LightningSubDiv, [224] = EmitterParam.LightningZigZag, [228] = EmitterParam.Detonate,
        [464] = EmitterParam.AlphaMid, [468] = EmitterParam.ColorMid, [480] = EmitterParam.PercentStart, [481] = EmitterParam.PercentMid,
        [482] = EmitterParam.PercentEnd, [484] = EmitterParam.SizeMid, [488] = EmitterParam.SizeMidY,
    };

    /// <summary>
    /// The emitter header follows the node header: deadspace, blast radius and length (floats), grid (two uint32),
    /// spawn type, update, render and blend names (32 bytes each), texture (64), chunk model (16), two-sided texture,
    /// loop, render order (uint16, then two unused bytes) and the flag word.
    /// </summary>
    EmitterData ReadEmitter(int o, Dictionary<int, Keys> keys)
    {
        uint flags = U(o + 0x144);
        var chunk = S(o + 0x128, 16);
        var e = new EmitterData
        {
            DeadSpace = F(o + 0x70),
            BlastRadius = F(o + 0x74),
            BlastLength = F(o + 0x78),
            GridX = (int)Math.Max(1, Math.Min(64, U(o + 0x7C))),
            GridY = (int)Math.Max(1, Math.Min(64, U(o + 0x80))),
            SpawnType = (int)U(o + 0x84),
            Update = S(o + 0x88, 32),
            Render = S(o + 0xA8, 32),
            Blend = S(o + 0xC8, 32),
            Texture = S(o + 0xE8, 64),
            Chunk = chunk.Equals("null", StringComparison.OrdinalIgnoreCase) ? "" : chunk,
            TwoSided = U(o + 0x138) != 0,
            Loop = U(o + 0x13C) != 0,
            RenderOrder = BinaryPrimitives.ReadUInt16LittleEndian(_b.AsSpan(12 + o + 0x140)),
            P2P = (flags & 0x1) != 0,
            P2PBezier = (flags & 0x2) != 0,
            AffectedByWind = (flags & 0x4) != 0,
            Tinted = (flags & 0x8) != 0,
            Bounce = (flags & 0x10) != 0,
            Random = (flags & 0x20) != 0,
            Inherit = (flags & 0x40) != 0,
            InheritVel = (flags & 0x80) != 0,
            InheritLocal = (flags & 0x100) != 0,
            Splat = (flags & 0x200) != 0,
            InheritPart = (flags & 0x400) != 0,
            AlphaStart = 1, AlphaEnd = 1, RandomVelocity = 0, Spread = 0, Mass = 0,
        };
        if (e.SpawnType is < 0 or > 1) e.SpawnType = 0;
        foreach (var (type, k) in keys)
        {
            if (!EmitterParams.TryGetValue(type, out var param)) continue;
            if (EmitterData.IsColor(param))
            {
                if (k.Width < 3) continue;
                for (int c = 0; c < 3; c++) e.Set(param, k.Values[c], c);
            }
            else e.Set(param, k.Values[0]);
        }
        return e;
    }

    /// <summary>The first value column of a controller's keys.</summary>
    static float[] Column(Keys k)
    {
        var v = new float[k.Times.Length];
        for (int r = 0; r < v.Length; r++) v[r] = k.Values[r * k.Width];
        return v;
    }

    ModelAnimation ReadAnimation(int o)
    {
        var anim = new ModelAnimation
        {
            Name = S(o + 0x08, 64),
            Length = F(o + 0x70),
            Transition = F(o + 0x74),
            Root = S(o + 0x78, 64),
        };
        int events = (int)U(o + 0xB8);
        int eventCount = (int)U(o + 0xBC);
        for (int i = 0; i < eventCount && i < 256; i++)
            anim.Events.Add((F(events + i * 36), S(events + i * 36 + 4, 32)));
        ReadAnimNode((int)U(o + 0x48), anim, 0);
        return anim;
    }

    void ReadAnimNode(int o, ModelAnimation anim, int depth)
    {
        if (depth > 128 || o <= 0 || 12 + o + 0x70 > _b.Length) return;
        var keys = Controllers(o);
        uint flags = U(o + 0x6C);
        // Controller ids overlap between node kinds: birthrate is an emitter's, the multiplier a light's.
        Keys? birth = (flags & NodeEmitter) != 0 && keys.TryGetValue(CtrlEmBirthrate, out var b) ? b : null;
        Keys? multiplier = (flags & NodeLight) != 0 && keys.TryGetValue(CtrlLightMultiplier, out var lm) ? lm : null;
        Keys? color = (flags & NodeLight) != 0 && keys.TryGetValue(CtrlLightColor, out var lc) && lc.Width >= 3 ? lc : null;
        Keys? radius = (flags & NodeLight) != 0 && keys.TryGetValue(CtrlLightRadius, out var lr) ? lr : null;
        bool emitter = (flags & NodeEmitter) != 0;
        Keys? meshAlpha = (flags & NodeMesh) != 0 && (keys.TryGetValue(CtrlMeshAlpha, out var ma) || keys.TryGetValue(CtrlMeshAlphaAlt, out ma)) ? ma : null;
        if (keys.TryGetValue(CtrlPosition, out var pos) || keys.ContainsKey(CtrlOrientation) || birth != null || multiplier != null || color != null || radius != null
            || meshAlpha != null || keys.ContainsKey(CtrlScale) || emitter && keys.Keys.Any(EmitterParams.ContainsKey))
        {
            var track = new AnimationTrack();
            if (keys.TryGetValue(CtrlScale, out var scale)) { track.ScaleTimes = scale.Times; track.Scales = Column(scale); }
            if (emitter)
                foreach (var (type, k) in keys)
                    if (EmitterParams.TryGetValue(type, out var param) && (!EmitterData.IsColor(param) || k.Width >= 3))
                        track.EmitterKeys[param] = new KeyTrack { Times = k.Times, Values = k.Values, Width = k.Width };
            if (meshAlpha != null) { track.AlphaTimes = meshAlpha.Times; track.Alphas = Column(meshAlpha); }
            if (color != null)
            {
                track.LightColorTimes = color.Times;
                track.LightColors = new Vector3[color.Times.Length];
                for (int r = 0; r < color.Times.Length; r++) track.LightColors[r] = V3(color.Values, r);
            }
            if (birth != null) { track.BirthRateTimes = birth.Times; track.BirthRates = Column(birth); }
            if (multiplier != null) { track.LightTimes = multiplier.Times; track.LightMultipliers = Column(multiplier); }
            if (radius != null) { track.LightRadiusTimes = radius.Times; track.LightRadii = Column(radius); }
            if (pos != null && pos.Width >= 3)
            {
                track.PositionTimes = pos.Times;
                track.Positions = new Vector3[pos.Times.Length];
                for (int r = 0; r < pos.Times.Length; r++) track.Positions[r] = V3(pos.Values, r);
            }
            if (keys.TryGetValue(CtrlOrientation, out var rot) && rot.Width >= 4)
            {
                track.RotationTimes = rot.Times;
                track.Rotations = new Quaternion[rot.Times.Length];
                for (int r = 0; r < rot.Times.Length; r++) track.Rotations[r] = Q(rot.Values, r);
            }
            anim.Tracks[S(o + 0x20, 32)] = track;
        }
        int children = (int)U(o + 0x48);
        int count = (int)U(o + 0x4C);
        for (int i = 0; i < count; i++) ReadAnimNode((int)U(children + i * 4), anim, depth + 1);
    }
}
