using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Rendering;
using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>
/// EffectBeam: a beam from a body node of the effector to the target. visualeffects.2da gives
/// the beam's progfx row (ProgFX_Duration) and sound; progfx.2da type 7 rows name the beam model,
/// whose "Lightning" emitters are drawn as flickering jagged ribbons between the two ends (with the
/// emitter's texture, colour and width) and whose other emitters play at the source.
/// </summary>
public partial class BeamFx : Node
{
    public ClientSession Session = null!;

    sealed class Bolt
    {
        public required MeshInstance3D Mesh;
        public required ImmediateMesh Geometry;
        public float Width;
        public float Rate;
        public float LightningRadius;
        public bool LegacyFallback;
        public double NextShape;
        public System.Numerics.Vector3[] Path = Array.Empty<System.Numerics.Vector3>();
    }

    sealed class Beam
    {
        public required Node3D Root;
        public uint From, To;
        public int BodyPart;
        public bool Miss;
        public Vector3 MissOffset;
        public double Expires = double.MaxValue;
        public readonly List<Bolt> Bolts = new();
        public Node3D? SourceFx;
        public Node3D? TargetFx;
        public AudioStreamPlayer3D? Sound;
        public long EffectId;
        public (uint From, uint To, int Row, int BodyPart, bool Miss)? InstantKey;
    }

    readonly List<Beam> _beams = new();
    readonly Dictionary<long, Beam> _byEffect = new();
    readonly Dictionary<(uint From, uint To, int Row, int BodyPart, bool Miss), Beam> _instant = new();
    readonly Random _rng = new();
    double _now;

    /// <summary>Beams being drawn (capture checks).</summary>
    public int Count => _beams.Count;

    public void Clear()
    {
        foreach (var b in _beams) if (IsInstanceValid(b.Root)) b.Root.QueueFree();
        _beams.Clear();
        _byEffect.Clear();
        _instant.Clear();
    }

    public void Remove(long effectId)
    {
        if (effectId == 0 || !_byEffect.TryGetValue(effectId, out var beam)) return;
        End(beam);
    }

    void End(Beam beam)
    {
        _beams.Remove(beam);
        if (beam.EffectId != 0) _byEffect.Remove(beam.EffectId);
        if (beam.InstantKey is { } key) _instant.Remove(key);
        if (IsInstanceValid(beam.Root)) beam.Root.QueueFree();
    }

    public void Add(uint target, JsonElement d)
    {
        var content = Session.Content;
        if (content == null || !Session.Objects.ContainsKey(target)) return;
        int row = J.I(d, "vfx", -1);
        float duration = J.F(d, "duration");
        long id = J.L(d, "id");
        // Distinct body nodes and hit/miss placements remain separate beams.
        var key = (J.U(d, "from"), target, row, J.I(d, "bodyPart"), J.B(d, "miss"));
        bool instant = id == 0 && duration == 0;
        // No finite native expiry has been established. Bound repeated instant
        // applications without stacking geometry and looping audio indefinitely.
        // Retained effects keep their independent IDs and removal semantics.
        var vfx = content.Resources.TwoDA("visualeffects");
        if (instant && _instant.TryGetValue(key, out var existing))
        {
            // The geometry is not stacked, but each application still plays its one-shot impact.
            if (vfx.Get(row, "SoundDuration").Length == 0)
                PlaySound(existing.Root, content.Audio.Get(vfx.Get(row, "SoundImpact")), oneShot: true);
            return;
        }
        int prog = vfx.GetInt(row, "ProgFX_Duration", -1);
        if (prog < 0) prog = vfx.GetInt(row, "ProgFX_Impact", -1);
        var progfx = content.Resources.TwoDA("progfx");
        if (prog < 0 || progfx.GetInt(prog, "Type", -1) != 7) return;
        var modelName = progfx.Get(prog, "Param1");
        var model = content.Models.Get(modelName);
        if (model == null) return;

        var beam = new Beam
        {
            Root = new Node3D { Name = $"Beam_{row}_{target:X}" },
            From = J.U(d, "from"), To = target, BodyPart = J.I(d, "bodyPart"), Miss = J.B(d, "miss"),
        };
        if (beam.Miss) beam.MissOffset = new Vector3((float)_rng.NextDouble() * 4 - 2, (float)_rng.NextDouble() * 4 - 2, (float)_rng.NextDouble() * 1.5f);
        Session.Space.AddChild(beam.Root);
        foreach (var part in model.Parts)
        {
            if (part.Emitter is not { } e) continue;
            if (e.Update.Equals("Lightning", StringComparison.OrdinalIgnoreCase)) beam.Bolts.Add(MakeBolt(e, content));
        }
        bool boundPointToPoint = false;
        foreach (var decoration in BeamModelLayout.Decorations(model))
        {
            if (EmitterBuilder.Build(decoration.Data, content.Textures) is { } fx)
            {
                // Native source decorations retain their complete parent transforms.
                fx.Transform = Rendering.Convert.T(decoration.Transform);
                if (decoration.Targeted)
                {
                    beam.TargetFx ??= new Node3D { Name = "Target" };
                    fx.BindTarget(beam.TargetFx);
                    boundPointToPoint = true;
                }
                beam.SourceFx ??= new Node3D { Name = "Source" };
                beam.SourceFx.AddChild(fx);
            }
        }
        foreach (var reference in BeamModelLayout.EndpointReferences(model))
        {
            if (content.Models.Get(reference.Model) is not { } endpoint) continue;
            // The measured reattachable path follows the target body node. Custom
            // reference offsets/orientation, meshes/lights, submodel animation and
            // non-reattachable or P2P reference placement remain separate.
            var built = ModelBuilder.BuildStatic(endpoint, content.Materials,
                lights: false, meshes: false, effect: true);
            beam.TargetFx ??= new Node3D { Name = "Target" };
            beam.TargetFx.AddChild(built.Root);
        }
        if (beam.TargetFx != null) beam.Root.AddChild(beam.TargetFx);
        if (beam.SourceFx != null) beam.Root.AddChild(beam.SourceFx);
        if (BeamModelLayout.NeedsLegacyConnector(model, boundPointToPoint)) beam.Bolts.Add(MakeBolt(new EmitterData { Texture = "fxpa_white", Blend = "Lighten", SizeStart = 0.08f, BirthRate = 10, ColorStart = System.Numerics.Vector3.One }, content, legacyFallback: true));
        foreach (var b in beam.Bolts) beam.Root.AddChild(b.Mesh);

        // Native instant beams remain active until target removal in the ten-second
        // observation window. There is no measured 1.5-second default expiry.
        // Temporary effects end on time; permanent effects end on removal.
        if (duration > 0) beam.Expires = _now + duration;
        if (id != 0)
        {
            Remove(id);
            beam.EffectId = id;
            _byEffect[id] = beam;
        }
        if (instant) { beam.InstantKey = key; _instant[key] = beam; }
        _beams.Add(beam);

        var sound = vfx.Get(row, "SoundDuration");
        bool loop = sound.Length > 0;
        if (!loop) sound = vfx.Get(row, "SoundImpact");
        beam.Sound = PlaySound(beam.Root, loop ? content.Audio.GetLooping(sound) : content.Audio.Get(sound), oneShot: false);
        Update(beam, force: true);
    }

    static AudioStreamPlayer3D? PlaySound(Node3D parent, AudioStream? stream, bool oneShot)
    {
        if (stream == null) return null;
        // Native cold-beam PCM preserves the installed waveform. Godot's default
        // distance low-pass changes it; disable that extra filtering for beams.
        var player = new AudioStreamPlayer3D { Stream = stream, UnitSize = 6, MaxDistance = 40,
            AttenuationFilterCutoffHz = 20500, Bus = OpenWinter.Client.Preferences.AudioBuses.Effects3D };
        if (oneShot) player.Finished += player.QueueFree;
        parent.AddChild(player);
        player.Play();
        return player;
    }

    static Bolt MakeBolt(EmitterData e, ClientContent content, bool legacyFallback = false)
    {
        var tex = content.Textures.Get(e.Texture);
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            // Native Normal beams occlude the background; additive black disappears.
            BlendMode = e.Blend.Equals("Normal", StringComparison.OrdinalIgnoreCase)
                ? BaseMaterial3D.BlendModeEnum.Mix : BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoTexture = tex?.Texture,
            AlbedoColor = new Color(e.ColorStart.X, e.ColorStart.Y, e.ColorStart.Z, Math.Clamp(e.AlphaStart, 0.2f, 1f)),
            VertexColorUseAsAlbedo = true,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            DisableFog = true,
        };
        var geometry = new ImmediateMesh();
        return new Bolt
        {
            Geometry = geometry,
            Mesh = new MeshInstance3D { Name = "Bolt", Mesh = geometry, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off },
            Width = FxMath.BeamWidth(e.SizeStart, e.SizeEnd),
            Rate = Math.Clamp(e.BirthRate, 4f, 30f),
            LightningRadius = e.LightningRadius,
            LegacyFallback = legacyFallback,
        };
    }

    public override void _Process(double delta)
    {
        _now += delta;
        for (int i = _beams.Count - 1; i >= 0; i--)
        {
            var beam = _beams[i];
            if (_now >= beam.Expires || !IsInstanceValid(beam.Root) || !Session.Objects.ContainsKey(beam.To))
            {
                End(beam);
                continue;
            }
            Update(beam, force: false);
        }
    }

    /// <summary>A body node of a creature in area space, or a point at its chest.</summary>
    Vector3? NodePoint(uint id, params string[] nodes)
    {
        if (Session.Objects.GetValueOrDefault(id) is not { } view || !IsInstanceValid(view)) return null;
        if (view is CreatureView { Skeleton: { } sk })
            foreach (var name in nodes)
            {
                int bone = ModelBuilder.FindBone(sk, name);
                if (bone >= 0) return Session.Space.ToLocal(sk.GlobalTransform * sk.GetBoneGlobalPose(bone).Origin);
            }
        return Session.Space.ToLocal(view.GlobalPosition) + new Vector3(0, 0, view is CreatureView ? 1.2f : 0.8f);
    }

    /// <summary>BODY_NODE_HAND 0, CHEST 1, MONSTER_0..9 2-11.</summary>
    static string[] SourceNodes(int bodyPart) => bodyPart switch
    {
        0 => new[] { "rhand", "handconjure", "lhand", "impact" },
        1 => new[] { "impact", "torso_g", "chest" },
        >= 2 and <= 11 => new[] { $"monster{bodyPart - 2}", $"monster_{bodyPart - 2}", "rhand", "impact" },
        _ => new[] { "impact" },
    };

    void Update(Beam beam, bool force)
    {
        var from = NodePoint(beam.From, SourceNodes(beam.BodyPart));
        var to = NodePoint(beam.To, "impact", "torso_g", "chest");
        if (from == null && to == null) return;
        from ??= to!.Value + new Vector3(0, 0, 2);
        to ??= from.Value;
        var end = to.Value + beam.MissOffset;
        if (beam.SourceFx != null) beam.SourceFx.Position = from.Value;
        if (beam.TargetFx != null) beam.TargetFx.Position = end;
        // Endpoint-swap PCM controls place the sustained cue at the target.
        // It continues after source destruction, ending when the target disappears.
        if (beam.Sound != null) beam.Sound.Position = to.Value;
        var cam = GetViewport().GetCamera3D();
        var eye = cam != null ? Session.Space.ToLocal(cam.GlobalPosition) : from.Value + new Vector3(0, -10, 5);
        foreach (var bolt in beam.Bolts)
        {
            // A straight connector must follow both ends without leaving stale interior points.
            bool straight = !bolt.LegacyFallback && bolt.LightningRadius == 0;
            if (straight || force || _now >= bolt.NextShape || bolt.Path.Length == 0)
            {
                bolt.NextShape = _now + 1.0 / bolt.Rate;
                float length = from.Value.DistanceTo(end);
                int segments = Math.Clamp((int)(length / 0.45f), 4, 40);
                bolt.Path = FxMath.BeamPath(Rendering.Convert.N(from.Value), Rendering.Convert.N(end), segments,
                    bolt.LightningRadius, _rng, bolt.LegacyFallback);
            }
            else if (bolt.Path.Length > 1)
            {
                // Keep the ends attached while the creatures move.
                bolt.Path[0] = Rendering.Convert.N(from.Value);
                bolt.Path[^1] = Rendering.Convert.N(end);
            }
            Draw(bolt, eye);
        }
    }

    /// <summary>A camera-facing ribbon along the bolt's path; the (vertical) bolt texture stretches from end to end.</summary>
    static void Draw(Bolt bolt, Vector3 eye)
    {
        var g = bolt.Geometry;
        g.ClearSurfaces();
        var path = bolt.Path;
        if (path.Length < 2) return;
        g.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = 0; i < path.Length; i++)
        {
            var p = Rendering.Convert.V(path[i]);
            var tangent = Rendering.Convert.V(path[Math.Min(i + 1, path.Length - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
            var side = tangent.Cross(eye - p).Normalized() * (bolt.Width * 0.5f);
            float v = (float)i / (path.Length - 1);
            g.SurfaceSetColor(Colors.White);
            g.SurfaceSetUV(new Vector2(0, v));
            g.SurfaceAddVertex(p - side);
            g.SurfaceSetColor(Colors.White);
            g.SurfaceSetUV(new Vector2(1, v));
            g.SurfaceAddVertex(p + side);
        }
        g.SurfaceEnd();
    }
}
