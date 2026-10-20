using System.Text.Json;
using OpenWinter.Client.Rendering;
using OpenWinter.Client.Views.WorldFx.Logic;
using Godot;

namespace OpenWinter.Client.Views;

public partial class SpellFx
{
    readonly List<Projectile> _projectiles = new();
    readonly HashSet<AudioStreamPlayer3D> _projectileTails = new();

    sealed class Projectile
    {
        public required string Model;
        public string Sound = "", Spawn = "hand", Orientation = "path", Path = "homing";
        public Node3D? Node;
        public AudioStreamPlayer3D? Audio;
        public Vector3 From, To;
        public uint Caster, Target;
        public double Delay, Travel, Age;
        public long Presentation;
        public bool AwaitRelease;
        public bool ShowModel = true;
        public float Scale = 1;
        public Vector3 BurstOffset;
    }

    public override void _Ready() => Session.AreaLoading += OnLoading;
    void OnLoading(OpenWinter.Net.LoadAreaMessage _) => ClearProjectiles();

    public override void _ExitTree()
    {
        Session.AreaLoading -= OnLoading;
        ClearProjectiles();
    }

    void ClearProjectiles()
    {
        foreach (uint caster in _conjuring.Keys.Concat(_conjureHands.Keys).Distinct().ToArray()) StopConjure(caster);
        _conjureActions.Clear();
        _castPoses.Clear();
        _conjureLifetimes.Clear();
        _pendingReleases.Clear();
        foreach (var p in _projectiles)
        {
            if (IsInstanceValid(p.Node)) p.Node!.QueueFree();
            if (IsInstanceValid(p.Audio)) p.Audio!.QueueFree();
        }
        _projectiles.Clear();
        foreach (var audio in _projectileTails) if (IsInstanceValid(audio)) audio.QueueFree();
        _projectileTails.Clear();
    }

    void CancelPendingProjectiles(uint caster) => _projectiles.RemoveAll(p => p.Caster == caster && p.Node == null && (p.AwaitRelease || p.Age < p.Delay));

    static Vector3 Vec(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());

    void Launch(uint caster, JsonElement d)
    {
        string model = S(d, "model");
        if (model.Length == 0) return;
        _projectiles.Add(new Projectile
        {
            Model = model, Sound = S(d, "sound"), Spawn = S(d, "spawnPoint"), Orientation = S(d, "orientation"),
            Caster = caster, Target = d.TryGetProperty("target", out var target) ? target.GetUInt32() : uint.MaxValue,
            From = Vec(d.GetProperty("from")) + new Vector3(0, 0, 1.2f),
            To = Vec(d.GetProperty("to")) + new Vector3(0, 0, 1f), Path = S(d, "path"),
            Presentation = Presentation(d), AwaitRelease = Presentation(d) != 0,
            Delay = Presentation(d) != 0 ? 0 : d.TryGetProperty("delay", out var delay) ? delay.GetSingle() : 0,
            Travel = Math.Max(0, d.TryGetProperty("travel", out var travel) ? travel.GetSingle() : 1),
            ShowModel = !d.TryGetProperty("showModel", out var showModel) || showModel.GetBoolean(),
        });
    }

    // Magic Missile and the arrow/electric families are emitted by the impact scripts as
    // individual EffectVisualEffect calls. They have no spells.2da ProjModel. Each call must
    // launch exactly one missile from its effect creator, including delayed script calls.
    void Mirv(uint target, JsonElement d)
    {
        var resources = Session.Content?.Resources;
        if (resources == null || !d.TryGetProperty("creator", out var creator)) return;
        var vfx = resources.TwoDA("visualeffects");
        int row = d.GetProperty("vfx").GetInt32();
        var prog = resources.TwoDA("progfx");
        var spec = SpellProjectile.FromVisual(vfx, prog, row);
        if (spec == null) return;
        uint caster = creator.GetUInt32();
        var from = Anchor(caster, "handconjure", "rhand", "impact");
        var to = d.TryGetProperty("pos", out var point) ? Vec(point) : Anchor(target, "impact", "torso_g");
        // An unknown creator cannot be reconstructed from a target-only old payload.
        if (from == null || to == null) return;
        float distance = from.Value.DistanceTo(to.Value);
        // The installed acid-arrow script schedules its hit at distance / 25; Magic
        // Missile uses a logarithmic travel formula.
        float travel = spec.FlightSeconds(distance);
        _projectiles.Add(new Projectile
        {
            // EffectPlayer owns the visualeffects SoundDuration layer. ProgFX
            // Param2 has no measured spell mapping; do not source unrelated spell audio.
            Model = spec.Model,
            Path = spec.Path, Orientation = spec.Orientation,
            Caster = caster, Target = target, From = from.Value, To = to.Value,
            Travel = Math.Max(0.1f, travel),
            Scale = d.TryGetProperty("scale", out var scale) ? Math.Max(0.01f, scale.GetSingle()) : 1,
        });
    }

    Vector3? Anchor(uint id, params string[] nodes)
    {
        if (Session.Objects.GetValueOrDefault(id) is not { } view || !IsInstanceValid(view)) return null;
        if (view is CreatureView { Skeleton: { } sk })
            foreach (var node in nodes)
            {
                int bone = ModelBuilder.FindBone(sk, node);
                if (bone >= 0) return Session.Space.ToLocal(sk.GlobalTransform * sk.GetBoneGlobalPose(bone).Origin);
            }
        return Session.Space.ToLocal(view.GlobalPosition) + new Vector3(0, 0, view is CreatureView ? 1.2f : 0.5f);
    }

    bool StartProjectile(Projectile p)
    {
        // Native HasProjectile controls model visibility separately from Proj's
        // impact delay. Keep the existing sound/lifetime path for invisible models.
        var fx = p.ShowModel ? Effects.BuildModel(p.Model, burst: false) : new Node3D();
        if (fx == null) return false;
        p.Node = fx;
        Session.Space.AddChild(fx);
        p.From = p.Spawn.ToLowerInvariant() switch
        {
            "head" => Anchor(p.Caster, "headconjure", "head") ?? p.From,
            "hand" => Anchor(p.Caster, "handconjure", "rhand", "impact") ?? p.From,
            _ => p.From,
        };
        p.To = Anchor(p.Target, "impact", "torso_g") ?? p.To;
        // Native MIRVs fan upward before converging. The precise randomized native
        // spline remains unmeasured; this preserves its observed curved trajectory.
        float angle = (float)Random.Shared.NextDouble() * Mathf.Tau;
        float spread = Math.Min(p.From.DistanceTo(p.To) * 0.6f, 6f);
        p.BurstOffset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.5f) * spread;
        fx.Position = p.From;
        fx.Scale = Vector3.One * p.Scale;
        if (Session.Content?.Audio.Get(p.Sound) is { } stream)
        {
            var player = new AudioStreamPlayer3D
            {
                Stream = stream, MaxDistance = 40,
                Bus = OpenWinter.Client.Preferences.AudioBuses.Effects3D
            };
            // The sound follows the missile but is not owned by its visual model:
            // native restarts ProjSound on arrival, then lets the sample finish.
            Session.Space.AddChild(player);
            player.Position = p.From;
            p.Audio = player;
            player.Play();
        }
        return true;
    }

    public override void _Process(double delta)
    {
        // A dead/despawned caster cannot reach the server release callback. Retire
        // only pending presentation; already launched missiles keep their lifecycle.
        foreach (var key in _pendingReleases.Keys.ToArray())
            if (!Session.Objects.TryGetValue(key.Caster, out var caster)
                || (caster.Flags & OpenWinter.Net.ObjectFlags.Dead) != 0)
            {
                _pendingReleases.Remove(key);
                CancelPendingProjectiles(key.Caster);
            }
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            if (p.AwaitRelease) continue;
            p.Age += delta;
            if (p.Age < p.Delay) continue;
            // Create effects at release: an invisible pre-built model still ages its emitters,
            // flashes its lights and plays its audio before the projectile actually launches.
            if (p.Node == null && !StartProjectile(p) || !IsInstanceValid(p.Node))
            {
                if (IsInstanceValid(p.Audio)) p.Audio!.QueueFree();
                _projectiles.RemoveAt(i); continue;
            }
            var node = p.Node!;
            float t = p.Travel <= 0 ? 1 : (float)Math.Min(1, (p.Age - p.Delay) / p.Travel);
            var to = p.Path is "homing" or "burstup" ? Anchor(p.Target, "impact", "torso_g") ?? p.To : p.To;
            var pos = p.From.Lerp(to, p.Path == "accelerating" ? t * t : t);
            float arc = p.Path switch { "ballistic" => 2.5f, "highballistic" => 6f, _ => 0f };
            pos.Z += arc * 4 * t * (1 - t);
            if (p.Path == "burstup") pos += p.BurstOffset * (4 * t * (1 - t) * (1 - t));
            var dir = pos - node.Position;
            node.Position = pos;
            if (IsInstanceValid(p.Audio)) p.Audio!.Position = pos;
            if (p.Orientation == "target") dir = to - pos;
            if (p.Orientation is "path" or "target" && dir.LengthSquared() > 1e-6f)
                node.Quaternion = Rendering.Convert.Q(ProjectileOrientation.Rotation(Rendering.Convert.N(dir)));
            if (t < 1) continue;
            _projectiles.RemoveAt(i);
            if (IsInstanceValid(p.Audio))
            {
                var audio = p.Audio!;
                _projectileTails.Add(audio);
                audio.Finished += () => { _projectileTails.Remove(audio); audio.QueueFree(); };
                audio.Play();
            }
            Effects.EndEffect(node);
        }
    }
}
