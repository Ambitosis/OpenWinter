using OpenWinter.Client.Rendering.Emitters;
using OpenWinter.Content;
using Godot;
using N = System.Numerics;

namespace OpenWinter.Client.Rendering;

/// <summary>Turns ModelData into Godot nodes: static meshes, skinned skeletons, lights, emitters, animations.</summary>
public static class ModelBuilder
{
    /// <summary>Per-material vertex buffers being merged into one ArrayMesh surface.</summary>
    sealed class Batch
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<int> Indices = new();
        public readonly List<int> Bones = new();
        public readonly List<float> Weights = new();
    }

    public sealed class StaticModel
    {
        public required Node3D Root;
        /// <summary>Lights by model node name (tile main/source lights are recoloured by the area).</summary>
        public readonly List<(string Name, OmniLight3D Light)> Lights = new();
        /// <summary>
        /// Tile-fade geometry by role (<see cref="TileFade"/>) and, for neighbour plugs, the model-space
        /// edge they sit on (-1 = none). Fading parts start visible, caps and plugs hidden.
        /// </summary>
        public readonly List<(int Role, int Edge, MeshInstance3D Mesh)> Fades = new();
        /// <summary>
        /// Each mesh's triangles (three corners per face, in the mesh's space) by mesh instance name, as
        /// Mesh.GetFaces would give them, for collision shapes without reading the mesh back from the GPU.
        /// </summary>
        public readonly Dictionary<string, Vector3[]> Faces = new();
    }

    /// <summary>
    /// Strength of model and tile lights relative to their multiplier, measured on the original with the display
    /// curve corrected (parity/visual, scene tilelight).
    /// </summary>
    public const float LightScale = 0.88f;

    /// <summary>A model's committed meshes, for <see cref="BuildStatic"/> with shareMeshes.</summary>
    sealed class SharedMeshes
    {
        public readonly List<(string Name, ArrayMesh Mesh, int Role, int Edge)> Meshes = new();
        public readonly Dictionary<string, Vector3[]> Faces = new();
        public int Used;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ModelData, Dictionary<(MaterialFactory, MaterialKind), SharedMeshes>> Shared = new();

    /// <summary>Counts area loads, for letting go of shared meshes no recent area used (<see cref="NextArea"/>).</summary>
    public static int AreaGeneration { get; private set; }

    /// <summary>
    /// A new area is being built: shared meshes neither it nor the two areas before it used are let go, so going
    /// back through a door finds its meshes made while a long session doesn't keep every area's meshes.
    /// </summary>
    public static void NextArea()
    {
        AreaGeneration++;
        foreach (var (_, byKey) in Shared)
            lock (byKey)
                foreach (var key in byKey.Where(e => e.Value.Used < AreaGeneration - 2).Select(e => e.Key).ToList())
                    byKey.Remove(key);
    }

    /// <param name="effect">An effect model: explosions burst at once and nothing starts pre-warmed.</param>
    /// <param name="shareMeshes">Every copy of the model uses the same meshes (made the first time), as an area's
    /// tiles do: building each tile's meshes again was a large part of loading an area. The meshes must then not be
    /// changed (per-instance surface overrides and transparency are fine).</param>
    public static StaticModel BuildStatic(ModelData model, MaterialFactory materials, MaterialKind kind = MaterialKind.World, bool lights = true, bool emitters = true, bool meshes = true, bool effect = false, bool shareMeshes = false)
    {
        var root = new Node3D { Name = string.IsNullOrEmpty(model.Name) ? "Model" : model.Name };
        var result = new StaticModel { Root = root };
        var globals = model.RestGlobals();
        Dictionary<(MaterialFactory, MaterialKind), SharedMeshes>? sharing = shareMeshes && meshes ? Shared.GetOrCreateValue(model) : null;
        SharedMeshes? reuse = null;
        if (sharing != null) lock (sharing) sharing.TryGetValue((materials, kind), out reuse);
        if (reuse != null) { meshes = false; reuse.Used = AreaGeneration; }
        var solid = new Dictionary<Material, Batch>();
        // Fading geometry is grouped by role and edge, and fading parts each keep their own mesh: the
        // original fades the parts that come between the camera and the player, not whole tiles.
        var fade = new Dictionary<(int Role, int Edge, int Part), Dictionary<Material, Batch>>();
        for (int i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            var xf = Convert.T(globals[i]);
            if (meshes && Drawable(part.Mesh))
            {
                var mat = materials.Get(part.Mesh!, kind);
                if (mat != null)
                {
                    var target = solid;
                    if (part.TileFade)
                    {
                        int role = TileFade.Role(part.TileFadeMode);
                        var group = (role, role == TileFade.Neighbour ? FadeEdge(part.Mesh!, globals[i]) : -1, role == TileFade.Fade ? i : -1);
                        if (!fade.TryGetValue(group, out target!)) fade[group] = target = new();
                    }
                    if (!target.TryGetValue(mat, out var batch)) target[mat] = batch = new Batch();
                    Append(batch, part.Mesh!, xf, -1);
                }
            }
            if (lights && part.Light is { } l)
            {
                var light = MakeLight(part.Name, l);
                light.Transform = xf;
                root.AddChild(light);
                result.Lights.Add((part.Name, light));
            }
            if (emitters && part.Emitter is { } e)
            {
                // A model drawn without its animations shows its emitters as its idle animation sets them.
                var data = IdleAnimation(model)?.Tracks.GetValueOrDefault(part.Name) is { EmitterKeys.Count: > 0 } idle ? Sampled(e, idle) : e;
                var em = EmitterBuilder.Build(data, materials.Textures, new EmitterOptions { Warm = !effect, BurstUnkeyed = effect, Chunk = ChunkResolver(materials) });
                if (em != null)
                {
                    em.Name = part.Name;
                    em.Transform = xf;
                    root.AddChild(em);
                    if (ReferenceChild(model, i) is int child)
                    {
                        var target = new Node3D { Name = part.Name + "_ref", Transform = Convert.T(globals[child]) };
                        root.AddChild(target);
                        em.Target = target;
                    }
                }
            }
        }
        SharedMeshes? made = null;
        if (reuse != null)
        {
            foreach (var (name, mesh, role, edge) in reuse.Meshes) Place(name, mesh, role, edge);
            foreach (var (name, faces) in reuse.Faces) result.Faces[name] = faces;
            return result;
        }
        made = sharing != null ? new SharedMeshes { Used = AreaGeneration } : null;
        if (solid.Count > 0)
        {
            Place("Mesh", Commit(solid, false), -1, -1);
            result.Faces["Mesh"] = Faces(solid);
        }
        foreach (var ((role, edge, part), batches) in fade)
        {
            string name = $"Fade{role}_{edge}_{part}";
            Place(name, Commit(batches, false), role, edge);
            result.Faces[name] = Faces(batches);
        }
        if (made != null && sharing != null)
        {
            foreach (var (name, faces) in result.Faces) made.Faces[name] = faces;
            lock (sharing) sharing[(materials, kind)] = made;
        }
        return result;

        void Place(string name, ArrayMesh mesh, int role, int edge)
        {
            var mi = new MeshInstance3D { Name = name, Mesh = mesh };
            if (role >= 0)
            {
                mi.Visible = role == TileFade.Fade;
                result.Fades.Add((role, edge, mi));
            }
            root.AddChild(mi);
            made?.Meshes.Add((name, mesh, role, edge));
        }
    }

    /// <summary>The tile edge a mesh lies on, from its model-space bounds.</summary>
    static int FadeEdge(MeshData mesh, N.Matrix4x4 global)
    {
        if (mesh.Vertices.Length == 0) return -1;
        var min = new N.Vector3(float.MaxValue);
        var max = new N.Vector3(float.MinValue);
        foreach (var v in mesh.Vertices)
        {
            var p = N.Vector3.Transform(v, global);
            min = N.Vector3.Min(min, p);
            max = N.Vector3.Max(max, p);
        }
        return TileFade.EdgeOf(min, max);
    }

    /// <summary>
    /// A Skeleton3D with one bone per model node and every visible mesh skinned
    /// to it (rigid meshes bound 100% to their own node).
    /// <paramref name="textureFor"/> may substitute textures (PLT tinting).
    /// </summary>
    /// <param name="animations">The animations that will play on the skeleton (default: the model's own). Emitters are sized
    /// for the birthrates they key, and meshes whose opacity they key get their own instance to fade.</param>
    /// <param name="burstUnkeyed">Explosion emitters no animation keys burst at once (effect models).</param>
    public static Skeleton3D BuildSkinned(ModelData model, MaterialFactory materials, Func<ModelPart, Material?>? materialFor = null,
        IReadOnlyCollection<ModelAnimation>? animations = null, bool burstUnkeyed = false, bool warm = false, bool lights = false,
        Func<ModelPart, bool>? includeMesh = null)
    {
        animations ??= model.Animations.Values;
        var fading = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in animations)
            foreach (var (name, t) in a.Tracks)
                if (t.AlphaTimes.Length > 0 && model.FindPart(name) is var fp && fp >= 0 && model.Parts[fp].Mesh is { IsSkinned: false }) fading.Add(name);
        var skeleton = new Skeleton3D { Name = "Skeleton3D" };
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in model.Parts)
        {
            string name = string.IsNullOrEmpty(p.Name) ? "node" : p.Name;
            while (!used.Add(name)) name += "_";
            int b = skeleton.AddBone(name);
            if (p.Parent >= 0) skeleton.SetBoneParent(b, p.Parent);
            skeleton.SetBoneRest(b, Convert.Local(p.Position, p.Rotation));
        }
        skeleton.ResetBonePoses();

        var globals = model.RestGlobals();
        var skin = new Skin();
        for (int i = 0; i < model.Parts.Count; i++) skin.AddBind(i, Convert.T(globals[i]).AffineInverse());

        var batches = new Dictionary<Material, Batch>();
        for (int i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            if (!Drawable(part.Mesh) || fading.Contains(part.Name) || includeMesh?.Invoke(part) == false) continue;
            var mat = materialFor?.Invoke(part) ?? materials.Get(part.Mesh!, MaterialKind.Creature);
            if (mat == null) continue;
            if (!batches.TryGetValue(mat, out var batch)) batches[mat] = batch = new Batch();
            Append(batch, part.Mesh!, Convert.T(globals[i]), i);
        }
        if (batches.Count > 0)
        {
            var mi = new MeshInstance3D { Name = "Body", Mesh = Commit(batches, true), Skin = skin };
            skeleton.AddChild(mi);
            mi.Skeleton = new NodePath("..");
        }
        var attachments = new Dictionary<int, BoneAttachment3D>();
        BoneAttachment3D Attachment(int bone)
        {
            if (attachments.TryGetValue(bone, out var a)) return a;
            a = new BoneAttachment3D { Name = AttachmentName(skeleton, bone), BoneIdx = bone };
            skeleton.AddChild(a);
            return attachments[bone] = a;
        }
        for (int i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            if (!Drawable(part.Mesh) || !fading.Contains(part.Name) || includeMesh?.Invoke(part) == false) continue;
            // Faded by its animations through the instance's transparency; the material itself is opaque.
            var solid = new ModelPart { Name = part.Name, Mesh = part.Mesh!.Clone() };
            solid.Mesh.Alpha = 1f;
            var mat = materialFor?.Invoke(solid) ?? materials.Get(solid.Mesh, MaterialKind.Creature);
            if (mat == null) continue;
            Attachment(i).AddChild(new MeshInstance3D
            {
                Name = FadeMeshName, Mesh = MeshOf(part.Mesh, mat), Transparency = 1f - Math.Clamp(part.Mesh.Alpha, 0f, 1f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        // Lights on the skeleton follow their node, and animations key their colour and strength (effect flashes).
        if (lights)
            for (int i = 0; i < model.Parts.Count; i++)
                if (model.Parts[i].Light is { } l)
                {
                    // Effect lights reach their whole radius (Hellball's flash floods the area).
                    var light = MakeLight(LightName, l);
                    light.OmniAttenuation = EffectLightAttenuation;
                    Attachment(i).AddChild(light);
                }
        for (int i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            if (part.Emitter is not { } e) continue;
            var options = EmitterBuilder.OptionsFor(part, animations, materials, materials.Models, warm, burstUnkeyed);
            var em = EmitterBuilder.Build(e, materials.Textures, options);
            if (em == null) continue;
            Attachment(i).AddChild(em);
            if (ReferenceChild(model, i) is int child) em.Target = Attachment(child);
        }
        return skeleton;
    }

    public static OmniLight3D MakeLight(string name, LightData l) => new()
    {
        Name = name,
        LightColor = new Color(l.Color.X, l.Color.Y, l.Color.Z),
        OmniRange = Math.Max(0.5f, l.Radius),
        LightEnergy = Math.Clamp(l.Multiplier, 0.2f, 3f) * LightScale,
        ShadowEnabled = false,
        OmniAttenuation = 1.2f,
    };

    public const string LightName = "light";
    public static float EffectLightAttenuation = 0.5f;

    /// <summary>The node animations address a part's emitter, light and fading mesh through: "{bone}_fx".</summary>
    public static string AttachmentName(Skeleton3D skeleton, int bone) => skeleton.GetBoneName(bone) + "_fx";
    public const string FadeMeshName = "mesh";

    /// <summary>The reference node of a point-to-point or lightning emitter: its first child in the model.</summary>
    static int? ReferenceChild(ModelData model, int part)
    {
        for (int c = part + 1; c < model.Parts.Count; c++)
            if (model.Parts[c].Parent == part) return c;
        return null;
    }

    /// <summary>The animation a model without an animation player idles in (tiles and placeables built statically).</summary>
    static ModelAnimation? IdleAnimation(ModelData model) =>
        model.Animations.GetValueOrDefault("tiledefault") ?? model.Animations.GetValueOrDefault("default")
        ?? model.Animations.GetValueOrDefault("animloop01");

    /// <summary>The emitter as an animation leaves it: keyed values at its start, the birthrate at its highest.</summary>
    public static EmitterData Sampled(EmitterData e, AnimationTrack track)
    {
        var copy = e.Clone();
        foreach (var (param, keys) in track.EmitterKeys)
        {
            if (param == EmitterParam.BirthRate) { copy.BirthRate = keys.Max(); continue; }
            for (int c = 0; c < keys.Width && c < 3; c++) copy.Set(param, keys.Sample(0, c), c);
        }
        return copy;
    }

    static Func<string, (Mesh, Material?)?>? ChunkResolver(MaterialFactory materials) =>
        materials.Models == null ? null : EmitterBuilder.OptionsFor(new ModelPart { Emitter = new EmitterData() }, null, materials, materials.Models).Chunk;

    /// <summary>One mesh on its own, in its node's space.</summary>
    public static ArrayMesh MeshOf(MeshData mesh, Material? material = null)
    {
        var batch = new Batch();
        Append(batch, mesh, Transform3D.Identity, -1);
        var result = Commit(new Dictionary<Material, Batch> { [material ?? new StandardMaterial3D()] = batch }, false);
        if (material == null) result.SurfaceSetMaterial(0, null);
        return result;
    }

    /// <summary>
    /// Untextured meshes flagged translucent are editor/selection helpers
    /// (e.g. the "sam" box in door models) that the game never draws.
    /// </summary>
    public static bool Drawable(MeshData? mesh) =>
        mesh is { Render: true, IsWalkmesh: false } && mesh.Indices.Length > 0 && !(mesh.Texture.Length == 0 && mesh.TransparencyHint != 0);

    static void Append(Batch batch, MeshData mesh, Transform3D xf, int ownBone)
    {
        int baseIndex = batch.Vertices.Count;
        var normalBasis = xf.Basis.Inverse().Transposed();
        var normals = mesh.Normals.Length == mesh.Vertices.Length ? null : SmoothNormals(mesh);
        bool skinned = ownBone >= 0;
        bool weighted = skinned && mesh.IsSkinned;
        for (int v = 0; v < mesh.Vertices.Length; v++)
        {
            batch.Vertices.Add(xf * Convert.V(mesh.Vertices[v]));
            var n = normals != null ? normals[v] : Convert.V(mesh.Normals[v]);
            batch.Normals.Add((normalBasis * n).Normalized());
            batch.Uvs.Add(v < mesh.Uvs.Length ? Convert.V(mesh.Uvs[v]) : Vector2.Zero);
            if (!skinned) continue;
            if (weighted)
            {
                float total = 0;
                for (int k = 0; k < 4; k++) if (mesh.BoneParts[v * 4 + k] >= 0) total += mesh.BoneWeights[v * 4 + k];
                if (total <= 0)
                {
                    batch.Bones.AddRange(new[] { ownBone, 0, 0, 0 });
                    batch.Weights.AddRange(new[] { 1f, 0, 0, 0 });
                    continue;
                }
                for (int k = 0; k < 4; k++)
                {
                    int b = mesh.BoneParts[v * 4 + k];
                    batch.Bones.Add(Math.Max(0, b));
                    batch.Weights.Add(b >= 0 ? mesh.BoneWeights[v * 4 + k] / total : 0);
                }
            }
            else
            {
                batch.Bones.AddRange(new[] { ownBone, 0, 0, 0 });
                batch.Weights.AddRange(new[] { 1f, 0, 0, 0 });
            }
        }
        foreach (var i in mesh.Indices) batch.Indices.Add(baseIndex + i);
    }

    static Vector3[] SmoothNormals(MeshData mesh)
    {
        var result = new Vector3[mesh.Vertices.Length];
        for (int f = 0; f + 2 < mesh.Indices.Length; f += 3)
        {
            int a = mesh.Indices[f], b = mesh.Indices[f + 1], c = mesh.Indices[f + 2];
            var va = Convert.V(mesh.Vertices[a]);
            // Clockwise winding: (c - a) x (b - a) faces the front.
            var n = (Convert.V(mesh.Vertices[c]) - va).Cross(Convert.V(mesh.Vertices[b]) - va);
            result[a] += n; result[b] += n; result[c] += n;
        }
        for (int i = 0; i < result.Length; i++) result[i] = result[i].LengthSquared() > 0 ? result[i].Normalized() : Vector3.Up;
        return result;
    }

    /// <summary>The triangles of what <see cref="Commit"/> makes of these batches, surface by surface.</summary>
    static Vector3[] Faces(Dictionary<Material, Batch> batches)
    {
        var faces = new Vector3[batches.Values.Sum(b => b.Indices.Count / 3 * 3)];
        int n = 0;
        foreach (var b in batches.Values)
            for (int i = 0; i + 2 < b.Indices.Count; i += 3)
            {
                faces[n++] = b.Vertices[b.Indices[i]];
                faces[n++] = b.Vertices[b.Indices[i + 1]];
                faces[n++] = b.Vertices[b.Indices[i + 2]];
            }
        return faces;
    }

    static ArrayMesh Commit(Dictionary<Material, Batch> batches, bool skinned)
    {
        var mesh = new ArrayMesh();
        foreach (var (material, b) in batches)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = b.Vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = b.Normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = b.Uvs.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = b.Indices.ToArray();
            if (skinned)
            {
                arrays[(int)Mesh.ArrayType.Bones] = b.Bones.ToArray();
                arrays[(int)Mesh.ArrayType.Weights] = b.Weights.ToArray();
            }
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, material);
        }
        return mesh;
    }

    /// <summary>Animation targeting skeleton bones by name. Position keys may be rescaled (supermodel animations).</summary>
    public static Animation BuildAnimation(ModelAnimation anim, Skeleton3D skeleton, string skeletonPath, float positionScale)
    {
        var a = new Animation { Length = Math.Max(anim.Length, 0.01f) };
        foreach (var (partName, track) in anim.Tracks)
        {
            int bone = FindBone(skeleton, partName);
            if (bone < 0) continue;
            var path = new NodePath($"{skeletonPath}:{skeleton.GetBoneName(bone)}");
            if (track.PositionTimes.Length > 0)
            {
                int t = a.AddTrack(Animation.TrackType.Position3D);
                a.TrackSetPath(t, path);
                for (int k = 0; k < track.PositionTimes.Length; k++)
                    a.PositionTrackInsertKey(t, track.PositionTimes[k], Convert.V(track.Positions[k]) * positionScale);
            }
            if (track.RotationTimes.Length > 0)
            {
                int t = a.AddTrack(Animation.TrackType.Rotation3D);
                a.TrackSetPath(t, path);
                for (int k = 0; k < track.RotationTimes.Length; k++)
                    a.RotationTrackInsertKey(t, track.RotationTimes[k], Convert.Q(track.Rotations[k]));
            }
            if (track.ScaleTimes.Length > 0)
            {
                int t = a.AddTrack(Animation.TrackType.Scale3D);
                a.TrackSetPath(t, path);
                for (int k = 0; k < track.ScaleTimes.Length; k++)
                    a.ScaleTrackInsertKey(t, track.ScaleTimes[k], Vector3.One * track.Scales[k]);
            }
            // Emitter controllers and mesh opacity: value tracks on the part's attachment.
            string attach = AttachmentName(skeleton, bone);
            if (track.EmitterKeys.Count > 0 && skeleton.GetNodeOrNull($"{attach}/{AuroraEmitter.NodeName}") != null)
                foreach (var (param, keys) in track.EmitterKeys)
                {
                    int t = a.AddTrack(Animation.TrackType.Value);
                    a.TrackSetPath(t, new NodePath($"{skeletonPath}/{attach}/{AuroraEmitter.NodeName}:{AuroraEmitter.Property(param)}"));
                    a.ValueTrackSetUpdateMode(t, Animation.UpdateMode.Continuous);
                    bool color = EmitterData.IsColor(param) && keys.Width >= 3;
                    for (int k = 0; k < keys.Times.Length; k++)
                    {
                        Variant v = color
                            ? new Color(keys.Values[k * keys.Width], keys.Values[k * keys.Width + 1], keys.Values[k * keys.Width + 2])
                            : keys.Values[k * keys.Width];
                        a.TrackInsertKey(t, keys.Times[k], v);
                    }
                }
            if (skeleton.GetNodeOrNull($"{attach}/{LightName}") != null)
            {
                if (track.LightRadiusTimes.Length > 0)
                {
                    int t = a.AddTrack(Animation.TrackType.Value);
                    a.TrackSetPath(t, new NodePath($"{skeletonPath}/{attach}/{LightName}:omni_range"));
                    a.ValueTrackSetUpdateMode(t, Animation.UpdateMode.Continuous);
                    for (int k = 0; k < track.LightRadiusTimes.Length; k++)
                        a.TrackInsertKey(t, track.LightRadiusTimes[k], Math.Max(0.5f, track.LightRadii[k]));
                }
                if (track.LightColorTimes.Length > 0)
                {
                    int t = a.AddTrack(Animation.TrackType.Value);
                    a.TrackSetPath(t, new NodePath($"{skeletonPath}/{attach}/{LightName}:light_color"));
                    a.ValueTrackSetUpdateMode(t, Animation.UpdateMode.Continuous);
                    for (int k = 0; k < track.LightColorTimes.Length; k++)
                        a.TrackInsertKey(t, track.LightColorTimes[k], new Color(track.LightColors[k].X, track.LightColors[k].Y, track.LightColors[k].Z));
                }
                if (track.LightTimes.Length > 0)
                {
                    int t = a.AddTrack(Animation.TrackType.Value);
                    a.TrackSetPath(t, new NodePath($"{skeletonPath}/{attach}/{LightName}:light_energy"));
                    a.ValueTrackSetUpdateMode(t, Animation.UpdateMode.Continuous);
                    for (int k = 0; k < track.LightTimes.Length; k++)
                        a.TrackInsertKey(t, track.LightTimes[k], Math.Clamp(track.LightMultipliers[k], 0f, 3f));
                }
            }
            if (track.AlphaTimes.Length > 0 && skeleton.GetNodeOrNull($"{attach}/{FadeMeshName}") != null)
            {
                int t = a.AddTrack(Animation.TrackType.Value);
                a.TrackSetPath(t, new NodePath($"{skeletonPath}/{attach}/{FadeMeshName}:transparency"));
                a.ValueTrackSetUpdateMode(t, Animation.UpdateMode.Continuous);
                for (int k = 0; k < track.AlphaTimes.Length; k++)
                    a.TrackInsertKey(t, track.AlphaTimes[k], 1f - Math.Clamp(track.Alphas[k], 0f, 1f));
            }
        }
        var detonations = anim.Events.Where(e => e.Name.Equals("detonate", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Time).Distinct().Order().ToArray();
        if (detonations.Length > 0)
            foreach (var emitter in skeleton.FindChildren("*", "Node3D", true, false).OfType<AuroraEmitter>().Where(e => e.Data.Is("Explosion")))
            {
                int t = a.AddTrack(Animation.TrackType.Method);
                a.TrackSetPath(t, new NodePath($"{skeletonPath}/{skeleton.GetPathTo(emitter)}"));
                foreach (float time in detonations)
                    a.TrackInsertKey(t, time, new Godot.Collections.Dictionary
                    {
                        ["method"] = nameof(AuroraEmitter.DetonateEvent),
                        ["args"] = new Godot.Collections.Array(),
                    });
            }
        return a;
    }

    public static int FindBone(Skeleton3D skeleton, string name)
    {
        int b = skeleton.FindBone(name);
        if (b >= 0) return b;
        for (int i = 0; i < skeleton.GetBoneCount(); i++)
            if (string.Equals(skeleton.GetBoneName(i), name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
