using OpenWinter.Content;
using OpenWinter.Client.Views.WorldFx.Logic;
using Godot;

namespace OpenWinter.Client.Rendering.Emitters;

/// <summary>What a model's animations do to an emitter, measured before it is built.</summary>
public struct EmitterRange
{
    /// <summary>The largest birthrate and life any animation keys (the particle pool is sized for them).</summary>
    public float MaxBirthRate, MaxLife;
    /// <summary>Whether some animation keys the birthrate (explosions then wait for their keys).</summary>
    public bool BirthRateKeyed;
    /// <summary>Explicit detonation events own explosion timing, rather than the legacy birthrate fallback.</summary>
    public bool DetonationEvents;

    public static EmitterRange Of(EmitterData e, string part, IEnumerable<ModelAnimation>? animations)
    {
        var r = new EmitterRange { MaxBirthRate = e.BirthRate, MaxLife = e.LifeExpectancy };
        if (animations == null) return r;
        foreach (var a in animations)
        {
            r.DetonationEvents |= a.Events.Any(e => e.Name.Equals("detonate", StringComparison.OrdinalIgnoreCase));
            if (!a.Tracks.TryGetValue(part, out var t)) continue;
            if (t.EmitterKeys.TryGetValue(EmitterParam.BirthRate, out var b)) { r.MaxBirthRate = Math.Max(r.MaxBirthRate, b.Max()); r.BirthRateKeyed = true; }
            if (t.EmitterKeys.TryGetValue(EmitterParam.LifeExp, out var l)) r.MaxLife = Math.Max(r.MaxLife, l.Max());
        }
        return r;
    }
}

/// <summary>How an emitter is built.</summary>
public sealed class EmitterOptions
{
    public EmitterRange Range;
    /// <summary>Start with a full set of particles (ambient fires and smoke in an area that just loaded).</summary>
    public bool Warm;
    /// <summary>Explosions no animation keys burst when built (one-shot effect models); otherwise they wait.</summary>
    public bool BurstUnkeyed;
    /// <summary>Debris meshes for chunk emitters (resolved by the caller from the chunk model).</summary>
    public Func<string, (Mesh Mesh, Material? Material)?>? Chunk;
}

/// <summary>
/// One Aurora emitter. The node keeps the emitter's live values; animations change them through the "p_*"
/// properties (value tracks built by <see cref="ModelBuilder.BuildAnimation"/>), and the change reaches the GPU
/// as shader parameters on the next frame. Fountain, Single and Explosion emitters are GPU particles
/// (<see cref="EmitterShaders"/>); Linked emitters are ribbons and Lightning emitters bolts, both built on the CPU.
/// </summary>
public partial class AuroraEmitter : Node3D
{
    public const string NodeName = "em";
    public EmitterData Data = new();
    public EmitterOptions Options = new();
    /// <summary>The reference node for point-to-point particles and lightning (the emitter's first child in the model).</summary>
    public Node3D? Target;
    bool _boundTarget;
    public bool HasBoundTarget => _boundTarget;

    /// <summary>Bind an owned reference after construction, refreshing the GPU route before its next frame.</summary>
    public void BindTarget(Node3D target)
    {
        Target = target;
        _boundTarget = true;
        MarkDirty();
    }
    GpuParticles3D? _particles;
    ShaderMaterial? _process;
    LinkedRibbon? _ribbon;
    LightningBolt? _bolt;
    bool _dirty, _explosion, _local, _manualTrail;
    bool _trailHasPrevious;
    Transform3D _trailPrevious;
    double _trailCarry;
    bool _emissionEnabled = true;
    bool _primaryBurstActive;
    // Trails start still: nothing until the emitter moves.
    float _lastBirthRate;

    static readonly Dictionary<string, EmitterParam> ParamByProperty =
        Enum.GetValues<EmitterParam>().ToDictionary(p => Property(p), p => p);

    /// <summary>The animatable property for a controller ("p_AlphaStart").</summary>
    public static string Property(EmitterParam p) => "p_" + p;

    public static AuroraEmitter? Create(EmitterData source, TextureFactory textures, EmitterOptions? options = null)
    {
        var e = source.Clone();
        options ??= new EmitterOptions();
        var node = new AuroraEmitter { Name = NodeName, Data = e, Options = options };
        if (e.Is("Lightning"))
        {
            node._bolt = new LightningBolt(e, textures) { Name = "Bolt" };
            node.AddChild(node._bolt);
            return node;
        }
        if (e.Render.StartsWith("Linked", StringComparison.OrdinalIgnoreCase) && !e.Is("Single"))
        {
            node._ribbon = new LinkedRibbon(e, textures, options) { Name = "Ribbon" };
            node.AddChild(node._ribbon);
            return node;
        }
        var p = node.BuildParticles(textures);
        if (p == null) return null;
        node._particles = p;
        node.AddChild(p);
        return node;
    }

    GpuParticles3D? BuildParticles(TextureFactory textures)
    {
        var e = Data;
        bool single = e.Is("Single");
        _explosion = e.Is("Explosion");
        float maxLife = Math.Max(e.LifeExpectancy, Options.Range.MaxLife);
        float life = single && maxLife <= 0 ? 3600f : Math.Clamp(maxLife, 0.05f, 60f);
        float maxRate = Math.Max(e.BirthRate, Options.Range.MaxBirthRate);
        if (!single && maxRate <= 0) return null;
        _manualTrail = e.SpawnType == 1 && e.Is("Fountain") && !e.Inherit && !e.P2P;
        // Manual spatial births share the existing 2000-particle safety ceiling.
        // Reserve capacity once: resizing a live Godot pool destroys its particles.
        int amount = _manualTrail ? ParticleTrail.MaxParticles : single ? 1 : _explosion ? (int)Math.Clamp(maxRate, 1, 500) : (int)Math.Clamp(Math.Ceiling(maxRate * life), 1, 2000);

        string render = e.Render;
        bool chunk = e.Chunk.Length > 0 && Options.Chunk?.Invoke(e.Chunk) != null;
        int processMode = chunk ? EmitterShaders.ProcessTumble
            : Is(render, "Billboard_to_Local_Z") ? EmitterShaders.ProcessLocalPlane
            : Is(render, "Aligned_to_Particle_Dir") ? EmitterShaders.ProcessAlignedPlane
            : Is(render, "Motion_Blur") ? EmitterShaders.ProcessStreak
            : EmitterShaders.ProcessSize;
        int drawMode = processMode switch
        {
            EmitterShaders.ProcessLocalPlane or EmitterShaders.ProcessAlignedPlane => EmitterShaders.DrawBasis,
            EmitterShaders.ProcessStreak => EmitterShaders.DrawStreak,
            _ => Is(render, "Billboard_to_World_Z") ? EmitterShaders.DrawWorldFlat
                : Is(render, "Aligned_to_World_Z") ? EmitterShaders.DrawUpright : EmitterShaders.DrawCamera,
        };
        // Single particles and inheriting emitters move with the emitter; the rest stay where they were born.
        _local = single || e.Inherit;
        _process = new ShaderMaterial { Shader = single ? EmitterShaders.SingleProcess : EmitterShaders.Process };
        _process.SetShaderParameter("mode", processMode);
        _process.SetShaderParameter("manual_trail", _manualTrail);
        Mesh mesh;
        if (chunk && Options.Chunk!(e.Chunk) is { } debris)
        {
            mesh = debris.Mesh;
            if (debris.Material != null) mesh = WithMaterial(debris.Mesh, debris.Material);
        }
        else mesh = DrawQuad(DrawMaterial(e, textures, drawMode));

        float speed = Math.Abs(e.Velocity) + Math.Abs(e.RandomVelocity);
        float g = Math.Abs(e.Mass) * Gravity;
        float reach = speed * life + 0.5f * g * life * life + Math.Max(e.SizeStart, e.SizeEnd) + Math.Max(e.XSize, e.YSize) / 200f + 2f;
        reach = Math.Min(reach, 200f);
        var p = new GpuParticles3D
        {
            Name = "Particles",
            Amount = amount,
            Lifetime = life,
            OneShot = _explosion,
            Explosiveness = _explosion ? 1f : 0f,
            ProcessMaterial = _process,
            DrawPass1 = mesh,
            LocalCoords = _local,
            Interpolate = true,
            FractDelta = true,
            DrawOrder = e.Blend.StartsWith("Lighten", StringComparison.OrdinalIgnoreCase) ? GpuParticles3D.DrawOrderEnum.Index : GpuParticles3D.DrawOrderEnum.ViewDepth,
            VisibilityAabb = new Aabb(new Vector3(-reach, -reach, -reach), new Vector3(2 * reach, 2 * reach, 2 * reach)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            SortingOffset = e.RenderOrder * 0.05f,
            Emitting = !_explosion || Options.BurstUnkeyed && !Options.Range.BirthRateKeyed && !Options.Range.DetonationEvents && e.BirthRate > 0,
            Preprocess = Options.Warm && !_explosion && !single ? life : 0,
        };
        _lastBirthRate = e.BirthRate;
        Push(p);
        return p;
    }

    static readonly Dictionary<(Mesh, Material), Mesh> MeshesWithMaterial = new();

    /// <summary>A chunk mesh drawn with a material, made once per pair and shared (duplicating the mesh for every
    /// debris emitter was most of the time an area's placeables took to build).</summary>
    static Mesh WithMaterial(Mesh mesh, Material material)
    {
        if (mesh is not ArrayMesh am) return mesh;
        if (MeshesWithMaterial.TryGetValue((mesh, material), out var made)) return made;
        var copy = (ArrayMesh)am.Duplicate();
        for (int s = 0; s < copy.GetSurfaceCount(); s++) copy.SurfaceSetMaterial(s, material);
        return MeshesWithMaterial[(mesh, material)] = copy;
    }

    const float Gravity = 9.8f;

    static bool Is(string render, string name) => render.StartsWith(name, StringComparison.OrdinalIgnoreCase);

    static readonly Dictionary<string, ShaderMaterial> DrawMaterials = new();
    static readonly Dictionary<ShaderMaterial, QuadMesh> DrawQuads = new();

    /// <summary>The particle quad drawn with a draw material, shared by every emitter using it (making one each was
    /// most of the time an area's fires and smoke took to build).</summary>
    static QuadMesh DrawQuad(ShaderMaterial material)
    {
        if (!DrawQuads.TryGetValue(material, out var quad))
            DrawQuads[material] = quad = new QuadMesh { Size = Vector2.One, Material = material };
        return quad;
    }

    /// <summary>Shared by emitters with the same texture, grid, blend and draw mode.</summary>
    static ShaderMaterial DrawMaterial(EmitterData e, TextureFactory textures, int drawMode)
    {
        string blend = e.Blend.StartsWith("Lighten", StringComparison.OrdinalIgnoreCase) ? "add"
            : e.Blend.StartsWith("Punch", StringComparison.OrdinalIgnoreCase) ? "cut" : "mix";
        bool twoSided = !Is(e.Render, "Aligned_to_Particle_Dir") || e.TwoSided;
        string key = $"{e.Texture}|{e.GridX}x{e.GridY}|{blend}|{drawMode}|{e.Tinted}|{twoSided}";
        if (DrawMaterials.TryGetValue(key, out var m)) return m;
        var tex = textures.Get(e.Texture);
        m = new ShaderMaterial { Shader = EmitterShaders.DrawShader(blend, e.Tinted, twoSided) };
        if (tex != null) m.SetShaderParameter("tex", tex.Texture);
        m.SetShaderParameter("has_tex", tex != null);
        m.SetShaderParameter("grid", new Vector2(Math.Max(1, e.GridX), Math.Max(1, e.GridY)));
        m.SetShaderParameter("mode", drawMode);
        m.SetShaderParameter("flip_v", tex?.FlipV ?? false);
        m.SetShaderParameter("tex_gamma", blend == "add" ? Gamma(e) : Gamma(e) / 2.2f);
        m.SetShaderParameter("alpha_gamma", blend == "mix" ? BlendedAlphaGamma : 1f);
        DrawMaterials[key] = m;
        return m;
    }

    public override void _Ready()
    {
        // Ground height and world down in the simulation's space, for gravity and bouncing.
        if (_process != null)
        {
            var down = Vector3.Down;
            if (_local) down = (GlobalTransform.Basis.Inverse() * Vector3.Down).Normalized();
            _process.SetShaderParameter("down", down);
            // The ground is taken to be the height of the object the emitter belongs to (the AuroraSpace child).
            Node3D ground = this;
            for (Node? n = GetParent(); n is Node3D n3; n = n.GetParent())
            {
                if (n.Name == "AuroraSpace") break;
                ground = n3;
            }
            float floor = _local ? (GlobalTransform.AffineInverse() * ground.GlobalPosition).Dot(-down) : ground.GlobalPosition.Y;
            _process.SetShaderParameter("floor_h", floor);
        }
        _lastPosition = GlobalPosition;
        SetProcess(_dirty || Continuous);
    }

    /// <summary>Emitters that need a look every frame: point-to-point targets move, trails emit as they move.</summary>
    bool Continuous => _particles != null && (Target != null && Data.P2P || Data.SpawnType == 1 && !_explosion);

    Vector3 _lastPosition;
    float _trailRatio;

    public override void _Process(double delta)
    {
        if (_boundTarget && !IsInstanceValid(Target))
        {
            // An explicitly owned target must not leave a stale GPU stream alive.
            SetEmitting(false);
            SetProcess(false);
            return;
        }
        if (Target != null && Data.P2P && _process != null && IsInstanceValid(Target))
            _process.SetShaderParameter("target", _local ? GlobalTransform.AffineInverse() * Target.GlobalPosition : Target.GlobalPosition);
        if (_manualTrail && _particles != null && delta > 0)
            EmitTrail((float)delta);
        else if (Data.SpawnType == 1 && _particles != null && delta > 0)
        {
            // "Trail" emitters only lay particles while they move (a glyph drawn on the floor, a fist's fire):
            // full rate from a metre a second.
            float speed = GlobalPosition.DistanceTo(_lastPosition) / (float)delta;
            _lastPosition = GlobalPosition;
            float ratio = Math.Clamp(speed, 0f, 1f);
            if (Math.Abs(ratio - _trailRatio) > 0.02f) { _trailRatio = ratio; _dirty = true; }
        }
        if (!_dirty) return;
        _dirty = false;
        if (_particles != null) Push(_particles);
        if (!Continuous) SetProcess(false);
    }

    void EmitTrail(float delta)
    {
        var current = GlobalTransform;
        if (_trailHasPrevious && _emissionEnabled && Data.BirthRate > 0)
        {
            var births = ParticleTrail.Advance(_trailPrevious.Origin.DistanceTo(current.Origin), Data.BirthRate, ref _trailCarry);
            for (int i = 0; i < births.Count; i++)
            {
                float fraction = births.Fraction(i);
                var at = _trailPrevious.InterpolateWith(current, fraction);
                _particles!.EmitParticle(at, Vector3.Zero, Colors.White, new Color(0, (1 - fraction) * delta, 0, 0),
                    (uint)(GpuParticles3D.EmitFlags.Position | GpuParticles3D.EmitFlags.RotationScale | GpuParticles3D.EmitFlags.Custom));
            }
        }
        _trailPrevious = current;
        _trailHasPrevious = true;
    }

    /// <summary>Sends the live values to the simulation.</summary>
    void Push(GpuParticles3D p)
    {
        var e = Data;
        var m = _process!;
        bool single = e.Is("Single");
        float maxRate = Math.Max(1e-3f, Math.Max(e.BirthRate, Options.Range.MaxBirthRate));
        if (!_explosion && !single)
        {
            float trail = e.SpawnType == 1 ? _trailRatio : 1f;
            p.AmountRatio = _manualTrail ? 1f : Math.Clamp(e.BirthRate / maxRate * trail, 0f, 1f);
            p.Emitting = !_manualTrail && _emissionEnabled && e.BirthRate > 0 && trail > 0;
        }
        else if (single) p.Visible = e.BirthRate > 0;
        m.SetShaderParameter("life", single && e.LifeExpectancy <= 0 ? -1f : Math.Max(0.01f, e.LifeExpectancy));
        m.SetShaderParameter("velocity", e.Velocity);
        m.SetShaderParameter("randvel", e.RandomVelocity);
        m.SetShaderParameter("spread", Math.Max(0, e.Spread));
        m.SetShaderParameter("area", new Vector2(Math.Max(0, e.XSize) / 200f, Math.Max(0, e.YSize) / 200f));
        m.SetShaderParameter("gravity", e.Mass * Gravity);
        m.SetShaderParameter("drag", Math.Max(0, e.Drag));
        float g = Gamma(e);
        m.SetShaderParameter("col_start", Linear(e.ColorStart, e.AlphaStart, g));
        m.SetShaderParameter("col_end", Linear(e.ColorEnd, e.AlphaEnd, g));
        m.SetShaderParameter("col_mid", Linear(e.ColorMid, e.AlphaMid, g));
        bool hasY = e.SizeStartY != 0 || e.SizeEndY != 0;
        m.SetShaderParameter("size_start", new Vector2(e.SizeStart, hasY ? e.SizeStartY : e.SizeStart));
        m.SetShaderParameter("size_end", new Vector2(e.SizeEnd, hasY ? e.SizeEndY : e.SizeEnd));
        m.SetShaderParameter("size_mid", new Vector2(e.SizeMid, hasY ? e.SizeMidY : e.SizeMid));
        m.SetShaderParameter("has_mid", e.HasMid);
        m.SetShaderParameter("percent", Percents(e));
        m.SetShaderParameter("rot_speed", e.ParticleRot);
        int frames = Math.Max(1, e.GridX * e.GridY);
        float f0 = Math.Clamp(e.FrameStart, 0, frames - 1), f1 = Math.Clamp(e.FrameEnd, 0, frames - 1);
        if (f1 < f0) f1 = f0;
        m.SetShaderParameter("frame_start", (float)Math.Floor(f0));
        m.SetShaderParameter("frame_end", (float)Math.Floor(f1));
        m.SetShaderParameter("fps", Math.Max(0, e.Fps));
        m.SetShaderParameter("random_frame", e.Random);
        m.SetShaderParameter("blur", Math.Max(0, e.BlurLength) * 0.02f);
        m.SetShaderParameter("bounce", e.Bounce);
        m.SetShaderParameter("bounce_co", e.BounceCo);
        m.SetShaderParameter("p2p", e.P2P && Target != null ? (e.P2PBezier ? 1 : 2) : 0);
        m.SetShaderParameter("bezier2", e.P2PBezier2);
        m.SetShaderParameter("bezier3", e.P2PBezier3);
        m.SetShaderParameter("grav", e.Grav);
    }

    /// <summary>The ramp points as fractions of the life (stored as 0-1 or as percentages).</summary>
    static Vector3 Percents(EmitterData e)
    {
        if (!e.HasMid) return new Vector3(0, 0.5f, 1);
        float S(float v) => v > 1f ? v / 100f : v;
        float a = S(e.PercentStart), b = S(e.PercentMid), c = S(e.PercentEnd);
        if (c <= a) c = 1;
        return new Vector3(a, Math.Clamp(b, a, c), c);
    }

    /// <summary>
    /// The power that turns content colours (display values) into the renderer's linear values, for particle
    /// colours and textures alike. Blended particles decode exactly (2.2). The original adds "Lighten" particles
    /// in display space, where overlapping particles saturate much sooner (a brazier's dozen flames make a
    /// yellow-white core); adding the undecoded values reproduces that (parity/visual, scenes throne and rings).
    /// </summary>
    public static float ColorGamma = 2.2f, AdditiveGamma = 1.0f;

    /// <summary>
    /// The original also blends "Normal" particles in display space: half-transparent red over a dark ground comes
    /// out half as bright, where blending decoded values gives three quarters. Raising the opacity to this power
    /// matches it over dark backgrounds (parity/visual, scene emitcal), where most smoke and spell effects are seen.
    /// </summary>
    public static float BlendedAlphaGamma = 2.2f;

    public static float Gamma(EmitterData e) => e.Blend.StartsWith("Lighten", StringComparison.OrdinalIgnoreCase) ? AdditiveGamma : ColorGamma;

    /// <summary>Content colours are display values, clamped as the original clamps vertex colours.</summary>
    public static Color Linear(System.Numerics.Vector3 c, float alpha, float gamma) => new(
        MathF.Pow(Math.Clamp(c.X, 0, 1), gamma), MathF.Pow(Math.Clamp(c.Y, 0, 1), gamma), MathF.Pow(Math.Clamp(c.Z, 0, 1), gamma), alpha);

    /// <summary>Sets one controller (animations, scripts) and schedules the GPU update.</summary>
    public void Set(EmitterParam param, float value, int channel = 0)
    {
        Data.Set(param, value, channel);
        if (param == EmitterParam.BirthRate) OnBirthRate(value);
        if (param == EmitterParam.Detonate && value > 0) Detonate();
        MarkDirty();
    }

    void OnBirthRate(float rate)
    {
        // Explosions burst when their keyed birthrate turns on.
        if (_explosion && !Options.Range.DetonationEvents && rate > 0 && _lastBirthRate <= 0) Detonate();
        _lastBirthRate = rate;
    }

    /// <summary>Fires an explosion emitter's burst now.</summary>
    public void Detonate()
    {
        if (_particles == null) return;
        _particles.Restart();
        _particles.Emitting = true;
    }

    /// <summary>Authored animation event; repeated bursts retain earlier live particles.</summary>
    public void DetonateEvent()
    {
        if (_particles == null || !_explosion || !_emissionEnabled || Data.BirthRate <= 0 || !IsInsideTree()) return;
        // A later burst must not erase particles still alive from the previous event.
        // Reuse the primary pool after it drains; overlapping bursts share the shader
        // and authored live controllers but have independent simulation state.
        bool primary = !_primaryBurstActive;
        var burst = primary ? _particles : (GpuParticles3D)_particles.Duplicate();
        if (!primary)
        {
            burst.Emitting = false;
            AddChild(burst);
        }
        burst.Amount = (int)Math.Clamp(Data.BirthRate, 1, 500);
        burst.AmountRatio = 1;
        burst.Restart();
        burst.Emitting = true;
        if (primary) _primaryBurstActive = true;
        GetTree().CreateTimer(burst.Lifetime + 0.1, processAlways: false).Timeout += () =>
        {
            if (primary) _primaryBurstActive = false;
            else if (IsInstanceValid(burst)) burst.QueueFree();
        };
    }

    /// <summary>Turns a continuous emitter on or off (effect timing).</summary>
    public void SetEmitting(bool on)
    {
        // Animation controllers and moving trails can dirty the emitter after
        // cessation; Push must not restart emission while old particles finish.
        if (_emissionEnabled != on) _trailHasPrevious = false;
        _emissionEnabled = on;
        if (_particles != null && !_explosion) _particles.Emitting = !_manualTrail && on && (Data.BirthRate > 0 || Data.Is("Single"));
        _ribbon?.SetEmitting(on);
        if (_bolt != null) _bolt.Visible = on;
    }

    void MarkDirty()
    {
        if (_dirty) return;
        _dirty = true;
        SetProcess(true);
    }

    public override bool _Set(StringName property, Variant value)
    {
        if (!ParamByProperty.TryGetValue(property.ToString(), out var p)) return false;
        if (EmitterData.IsColor(p))
        {
            var c = value.AsColor();
            Data.Set(p, c.R, 0); Data.Set(p, c.G, 1); Data.Set(p, c.B, 2);
            MarkDirty();
        }
        else Set(p, value.AsSingle());
        return true;
    }

    public override Variant _Get(StringName property)
    {
        if (!ParamByProperty.TryGetValue(property.ToString(), out var p)) return default;
        if (EmitterData.IsColor(p)) return new Color(Data.Get(p, 0), Data.Get(p, 1), Data.Get(p, 2));
        return Data.Get(p);
    }

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var p in Enum.GetValues<EmitterParam>())
            list.Add(new Godot.Collections.Dictionary
            {
                ["name"] = Property(p),
                ["type"] = (int)(EmitterData.IsColor(p) ? Variant.Type.Color : Variant.Type.Float),
                ["usage"] = (int)PropertyUsageFlags.None,
            });
        return list;
    }
}
