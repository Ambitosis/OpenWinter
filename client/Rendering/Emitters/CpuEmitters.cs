using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Rendering.Emitters;

/// <summary>Materials for the CPU-built emitter geometry (ribbons and bolts): vertex colours times the texture.</summary>
static class StripMaterials
{
    static readonly Dictionary<string, StandardMaterial3D> Cache = new();

    public static StandardMaterial3D Get(EmitterData e, TextureFactory textures)
    {
        bool add = e.Blend.StartsWith("Lighten", StringComparison.OrdinalIgnoreCase);
        string key = e.Texture + (add ? "|add" : "|mix");
        if (Cache.TryGetValue(key, out var m)) return m;
        var tex = textures.Get(e.Texture);
        m = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = add ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            AlbedoTexture = tex?.Texture,
            VertexColorUseAsAlbedo = true,
            DisableFog = add,
        };
        if (tex?.FlipV == true)
        {
            m.Uv1Scale = new Vector3(1, -1, 1);
            m.Uv1Offset = new Vector3(0, 1, 0);
        }
        Cache[key] = m;
        return m;
    }
}

/// <summary>
/// A "Linked" emitter: its particles, in the order they were born, joined into one camera-facing ribbon (sword
/// trails, streams). Simulated on the CPU: the ribbon has to know each particle's neighbours.
/// </summary>
public partial class LinkedRibbon : MeshInstance3D
{
    struct Particle { public Vector3 Pos, Vel, Birth; public float Age, Life; }

    readonly EmitterData _e;
    readonly ImmediateMesh _mesh = new();
    readonly List<Particle> _live = new();
    readonly Random _rng = new();
    float _owed;
    bool _emitting = true;

    public LinkedRibbon(EmitterData e, TextureFactory textures, EmitterOptions options)
    {
        _e = e;
        Mesh = _mesh;
        MaterialOverride = StripMaterials.Get(e, textures);
        CastShadow = ShadowCastingSetting.Off;
        TopLevel = true;
    }

    public void SetEmitting(bool on) => _emitting = on;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var e = _e;
        GlobalTransform = Transform3D.Identity;
        var owner = GetParent<AuroraEmitter>();
        var at = owner.GlobalTransform;
        var target = owner.Target;
        bool targeted = owner.HasBoundTarget && PointToPoint.Supports(e) && target != null;
        if (targeted && !IsInstanceValid(target))
        {
            _live.Clear();
            _mesh.ClearSurfaces();
            return;
        }
        float life = Math.Clamp(e.LifeExpectancy, 0.02f, 30f);
        if (_emitting && e.BirthRate > 0)
        {
            _owed += e.BirthRate * dt;
            int n = Math.Min((int)_owed, 200);
            _owed -= (int)_owed;
            for (int i = 0; i < n; i++)
            {
                var local = new Vector3(((float)_rng.NextDouble() * 2 - 1) * e.XSize / 200f, ((float)_rng.NextDouble() * 2 - 1) * e.YSize / 200f, 0);
                float c = Mathf.Lerp(MathF.Cos(Math.Min(e.Spread, MathF.Tau) * 0.5f), 1f, (float)_rng.NextDouble());
                float s = MathF.Sqrt(Math.Max(0, 1 - c * c)), phi = (float)_rng.NextDouble() * MathF.Tau;
                var dir = (at.Basis * new Vector3(s * MathF.Cos(phi), s * MathF.Sin(phi), c)).Normalized();
                float speed = e.Velocity + ((float)_rng.NextDouble() * 2 - 1) * e.RandomVelocity;
                _live.Add(new Particle { Pos = at * local, Birth = at * local, Vel = dir * speed, Life = life });
            }
        }
        var gravity = Vector3.Down * (e.Mass * 9.8f);
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var p = _live[i];
            p.Age += dt;
            if (p.Age >= p.Life) { _live.RemoveAt(i); continue; }
            if (targeted)
                p.Pos = Convert.V(PointToPoint.Position(Convert.N(p.Birth), Convert.N(target!.GlobalPosition), p.Age, p.Life));
            else
            {
                p.Vel += gravity * dt;
                p.Vel /= 1 + Math.Max(0, e.Drag) * dt;
                p.Pos += p.Vel * dt;
            }
            _live[i] = p;
        }
        _mesh.ClearSurfaces();
        if (_live.Count < 2) return;
        var cam = GetViewport().GetCamera3D();
        if (cam == null) return;
        var eye = cam.GlobalPosition;
        _mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = 0; i < _live.Count; i++)
        {
            var p = _live[i];
            float t = Math.Clamp(p.Age / p.Life, 0, 1);
            var tangent = (_live[Math.Min(i + 1, _live.Count - 1)].Pos - _live[Math.Max(i - 1, 0)].Pos).Normalized();
            var side = tangent.Cross(eye - p.Pos).Normalized();
            float width = Math.Max(0, Mathf.Lerp(e.SizeStart, e.SizeEnd, t));
            var color = AuroraEmitter.Linear(System.Numerics.Vector3.Lerp(e.ColorStart, e.ColorEnd, t), Math.Clamp(Mathf.Lerp(e.AlphaStart, e.AlphaEnd, t), 0, 1), AuroraEmitter.Gamma(e));
            float v = (float)i / (_live.Count - 1);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceSetUV(new Vector2(0, v));
            _mesh.SurfaceAddVertex(p.Pos - side * width * 0.5f);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceSetUV(new Vector2(1, v));
            _mesh.SurfaceAddVertex(p.Pos + side * width * 0.5f);
        }
        _mesh.SurfaceEnd();
    }
}

/// <summary>
/// A "Lightning" emitter: a jagged bolt from the emitter to its reference node (the emitter's child in the model),
/// reshaped every lightningDelay seconds and as wide as the particle size.
/// </summary>
public partial class LightningBolt : MeshInstance3D
{
    readonly EmitterData _e;
    readonly ImmediateMesh _mesh = new();
    readonly Random _rng = new();
    System.Numerics.Vector3[] _path = Array.Empty<System.Numerics.Vector3>();
    double _next;

    public LightningBolt(EmitterData e, TextureFactory textures)
    {
        _e = e;
        Mesh = _mesh;
        MaterialOverride = StripMaterials.Get(e, textures);
        CastShadow = ShadowCastingSetting.Off;
        TopLevel = true;
    }

    public override void _Process(double delta)
    {
        _mesh.ClearSurfaces();
        GlobalTransform = Transform3D.Identity;
        var owner = GetParent<AuroraEmitter>();
        if (owner.Target is not { } target || !IsInstanceValid(target) || _e.BirthRate <= 0) return;
        var from = owner.GlobalPosition;
        var to = target.GlobalPosition;
        float length = from.DistanceTo(to);
        if (length < 1e-3f) return;
        _next -= delta;
        if (_next <= 0 || _path.Length < 2)
        {
            _next = Math.Max(0.04f, _e.LightningDelay > 0 ? _e.LightningDelay : 0.1f);
            int segments = Math.Clamp((int)(length / 0.3f) + (int)_e.LightningSubDiv, 3, 48);
            float jitter = Math.Max(_e.LightningRadius, 0.02f) + length * Math.Clamp(_e.LightningScale, 0f, 1f) * 0.15f;
            _path = FxMath.LightningPath(Convert.N(from), Convert.N(to), segments, jitter, _rng);
        }
        else
        {
            _path[0] = Convert.N(from);
            _path[^1] = Convert.N(to);
        }
        var cam = GetViewport().GetCamera3D();
        if (cam == null) return;
        var eye = cam.GlobalPosition;
        float width = Math.Max(0.04f, Math.Max(_e.SizeStart, _e.SizeEnd));
        _mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = 0; i < _path.Length; i++)
        {
            var p = Convert.V(_path[i]);
            float t = (float)i / (_path.Length - 1);
            var tangent = Convert.V(_path[Math.Min(i + 1, _path.Length - 1)] - _path[Math.Max(i - 1, 0)]).Normalized();
            var side = tangent.Cross(eye - p).Normalized() * (width * 0.5f);
            var color = AuroraEmitter.Linear(System.Numerics.Vector3.Lerp(_e.ColorStart, _e.ColorEnd, t), Math.Clamp(Mathf.Lerp(_e.AlphaStart, _e.AlphaEnd, t), 0, 1), AuroraEmitter.Gamma(_e));
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceSetUV(new Vector2(0, t));
            _mesh.SurfaceAddVertex(p - side);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceSetUV(new Vector2(1, t));
            _mesh.SurfaceAddVertex(p + side);
        }
        _mesh.SurfaceEnd();
    }
}
