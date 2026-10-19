using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// Spellcasting visuals: conjure effects on the caster's head, hands and ground while the spell
/// is prepared (with the chant), cast effects when it is released, and projectiles flying to the
/// target. Models and sounds are the spells.2da columns the server sends.
/// </summary>
public partial class SpellFx : Node
{
    public ClientSession Session = null!;
    public EffectPlayer Effects = null!;

    /// <summary>Conjure visuals per caster, removed when the cast happens or the spell is lost.</summary>
    readonly Dictionary<uint, List<Node3D>> _conjuring = new();
    readonly Dictionary<(uint Caster, long Id), Action> _pendingReleases = new();
    readonly Dictionary<uint, (long Presentation, ulong Action)> _conjureActions = new();
    sealed record CastPose(long Presentation, Anim.CreatureAnimator Animator, ulong Action);
    readonly Dictionary<uint, CastPose> _castPoses = new();
    readonly Dictionary<uint, (long Presentation, ulong Revision)> _conjureLifetimes = new();
    readonly Dictionary<uint, List<Node3D>> _conjureHands = new();
    ulong _conjureLifetimeRevision;

    public void Handle(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "vfx":
            case "spell-conjure":
            case "spell-conjure-timing":
            case "spell-conjure-lifetime":
            case "spell-cast-timing":
            case "spell-cast":
            case "spell-release":
            case "spell-projectile":
            case "spell-interrupted":
            case "spell-countered":
                break;
            default:
                return;
        }
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        switch (m.Kind)
        {
            case "vfx": Mirv(m.Subject, d); break;
            case "spell-conjure": Conjure(m.Subject, d); break;
            case "spell-conjure-timing": RetimeConjure(m.Subject, d); break;
            case "spell-conjure-lifetime": RetimeConjureLifetime(m.Subject, d); break;
            case "spell-cast-timing": RetimeCastPose(m.Subject, d); break;
            case "spell-cast": Cast(m.Subject, d); break;
            case "spell-release": Release(m.Subject, d); break;
            case "spell-projectile": Launch(m.Subject, d); break;
            case "spell-interrupted": Interrupt(m.Subject, d); break;
            default: StopConjure(m.Subject); CancelPendingProjectiles(m.Subject); break;
        }
    }

    static string S(JsonElement d, string key) => d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    void Conjure(uint caster, JsonElement d)
    {
        StopConjure(caster);
        var view = Session.Objects.GetValueOrDefault(caster);
        var nodes = new List<Node3D>();
        // Models carry "headconjure" and "handconjure" nodes for exactly these effects.
        Place(nodes, S(d, "headVfx"), view, "headconjure", burst: false);
        var hands = new List<Node3D>();
        Place(hands, S(d, "handVfx"), view, "handconjure", burst: false);
        Place(nodes, S(d, "groundVfx"), view, "", burst: false);
        // Native casting sounds and chants finish even after release or cancellation.
        // They belong to the creature, not the shorter-lived conjure particles.
        Sound(null, S(d, "sound"), view);
        Sound(null, S(d, "voice"), view);
        bool timedHands = Presentation(d) != 0 && d.TryGetProperty("conjureLifetime", out var lifetime)
            && lifetime.ValueKind == JsonValueKind.Number;
        if (timedHands) _conjureHands[caster] = hands;
        else nodes.AddRange(hands);
        _conjuring[caster] = nodes;
        string conjureClip = S(d, "animation") switch { "hand" => "conjure1", "head" => "conjure2", _ => "" };
        if (Presentation(d) != 0 && conjureClip.Length > 0
            && view is CreatureView { Animator: { } animator }
            && animator.CaptureAction()?.Clip == animator.Resolve(conjureClip))
            _conjureActions[caster] = (Presentation(d), animator.ActionVersion);
        if (timedHands) ScheduleConjureEnd(caster, Presentation(d), d.GetProperty("conjureLifetime").GetSingle());
        // Safety net in case the cast notify never comes.
        float duration = d.TryGetProperty("duration", out var dur) ? dur.GetSingle() : 2f;
        GetTree().CreateTimer(duration + 4).Timeout += () => { if (_conjuring.TryGetValue(caster, out var list) && list == nodes) EndConjureModels(caster); };
    }

    void Cast(uint caster, JsonElement d)
    {
        _conjureActions.Remove(caster);
        if (WorldFx.WorldFxPlayer.Verbose) GD.Print($"[spell-audio] cast {caster}");
        // Native self/touch hand effects remain through recovery. Other casting
        // styles retain their existing ending until independently measured.
        // Correlated self-cast hands have separate ownership. Head/ground models
        // retain this legacy deadline until their native behavior is established.
        if (S(d, "animation").ToLowerInvariant() is "self" or "touch"
            && _conjuring.TryGetValue(caster, out var preparing))
        {
            // Transfer ownership from Conjure's missing-notification timeout without
            // recreating any model; a long authored recovery must not hit that guard.
            var conjuring = new List<Node3D>(preparing);
            _conjuring[caster] = conjuring;
            float release = d.TryGetProperty("duration", out var releaseDuration) ? releaseDuration.GetSingle() : 0;
            float recovery = d.TryGetProperty("recovery", out var recoveryDuration) ? recoveryDuration.GetSingle() : 0;
            GetTree().CreateTimer(Math.Max(0.001f, release + recovery)).Timeout += () =>
            {
                // Interruptions and later casts must not let this timer stop a new effect.
                if (_conjuring.TryGetValue(caster, out var current) && current == conjuring) EndConjureModels(caster);
            };
        }
        else StopConjure(caster);
        // Native Extract Brain uses the model's cspecial once, starting before
        // impact and returning upright before the 3.6-second action completes.
        // Item use keeps its drinking/reading animation; instant casts remain
        // outside this measured path. The clip comes from the current model,
        // including public ReplaceObjectAnimation overrides, not a spell ID.
        if (S(d, "animation").Equals("creature", StringComparison.OrdinalIgnoreCase)
            && d.TryGetProperty("duration", out var creatureDuration) && creatureDuration.GetSingle() > 0
            && (!d.TryGetProperty("item", out var creatureItem) || creatureItem.GetUInt32() == OpenWinter.Game.Objects.GameObject.InvalidId)
            && Session.Objects.GetValueOrDefault(caster) is CreatureView { Animator: { } creatureAnimator })
            creatureAnimator.PlayAction("cspecial");
        // The server selects the measured attack family from its current weapon;
        // this is body presentation only, not another combat attack or projectile.
        if (S(d, "attackAnimation") is { Length: > 0 } attackClip
            && Session.Objects.GetValueOrDefault(caster) is CreatureView { Animator: { } attackAnimator })
            attackAnimator.PlayAction(attackClip);
        if (Presentation(d) != 0 && S(d, "animation").Equals("self", StringComparison.OrdinalIgnoreCase)
            && Session.Objects.GetValueOrDefault(caster) is CreatureView { Animator: { } animator }
            && animator.CaptureAction() is { } playback && playback.Clip == animator.Resolve("castself"))
        {
            var pose = new CastPose(Presentation(d), animator, animator.ActionVersion);
            _castPoses[caster] = pose;
            ForgetFinishedCastPose(caster, pose, playback.Remaining);
        }
        // The notification starts the release animation. Its authored "cast" event
        // and the server's release boundary occur after duration, not at its start.
        // Capture strings now: the notification's JsonDocument is disposed on return.
        float delay = d.TryGetProperty("duration", out var duration) ? Math.Max(0, duration.GetSingle()) : 0;
        if (S(d, "animation").Equals("out", StringComparison.OrdinalIgnoreCase)
            && delay > 0
            && (!d.TryGetProperty("item", out var outItem) || outItem.GetUInt32() == OpenWinter.Game.Objects.GameObject.InvalidId)
            && Session.Objects.GetValueOrDefault(caster) is CreatureView { Animator: { } outAnimator })
            outAnimator.SetCreatureCastRelease(outAnimator.ActionVersion, delay);
        string head = S(d, "headVfx"), hand = S(d, "handVfx"), ground = S(d, "groundVfx"), sound = S(d, "sound");
        // Native ClearAllActions after conjuring does not suppress this committed cue.
        // Keep its timer independent of the caster's subsequent action queue.
        void Release() => PlayRelease(caster, head, hand, ground, sound);
        long presentation = Presentation(d);
        if (presentation != 0) _pendingReleases[(caster, presentation)] = Release;
        else if (delay <= 0) Release();
        else GetTree().CreateTimer(delay).Timeout += Release;
    }

    void RetimeConjure(uint caster, JsonElement d)
    {
        if (_conjureActions.TryGetValue(caster, out var action)
            && action.Presentation == Presentation(d)
            && Session.Objects.GetValueOrDefault(caster) is CreatureView { Animator: { } animator })
            animator.RetimeAction(action.Action, d.GetProperty("remaining").GetSingle());
    }

    void RetimeCastPose(uint caster, JsonElement d)
    {
        if (!_castPoses.TryGetValue(caster, out var pose) || pose.Presentation != Presentation(d)) return;
        if (!IsInstanceValid(pose.Animator)
            || Session.Objects.GetValueOrDefault(caster) is not CreatureView { Animator: { } animator }
            || animator != pose.Animator || animator.ActionVersion != pose.Action)
        {
            _castPoses.Remove(caster);
            return;
        }
        float remaining = d.GetProperty("remaining").GetSingle();
        if (WorldFx.WorldFxPlayer.Verbose)
            GD.Print($"[cast-pose] retime caster={caster} presentation={pose.Presentation} action={pose.Action} remaining={remaining:F3} clip={animator.CaptureAction()?.Clip}");
        animator.RetimeAction(pose.Action, remaining);
    }

    void ForgetFinishedCastPose(uint caster, CastPose pose, double remaining)
    {
        GetTree().CreateTimer(Math.Max(.05, remaining + .05)).Timeout += () =>
        {
            if (!_castPoses.TryGetValue(caster, out var current) || current != pose) return;
            if (IsInstanceValid(pose.Animator) && pose.Animator.ActionVersion == pose.Action
                && pose.Animator.CaptureAction() is { Remaining: > 0 } active)
                ForgetFinishedCastPose(caster, pose, active.Remaining);
            else _castPoses.Remove(caster);
        };
    }

    void RetimeConjureLifetime(uint caster, JsonElement d)
    {
        if (_conjureLifetimes.TryGetValue(caster, out var lifetime)
            && lifetime.Presentation == Presentation(d))
            ScheduleConjureEnd(caster, lifetime.Presentation, d.GetProperty("remaining").GetSingle());
    }

    void ScheduleConjureEnd(uint caster, long presentation, float remaining)
    {
        if (!_conjureHands.TryGetValue(caster, out var nodes)) return;
        if (remaining <= 0) { EndConjureHands(caster); return; }
        ulong revision = ++_conjureLifetimeRevision;
        _conjureLifetimes[caster] = (presentation, revision);
        if (WorldFx.WorldFxPlayer.Verbose)
            GD.Print($"[conjure-life] schedule caster={caster} presentation={presentation} revision={revision} remaining={remaining:F3} ticks={Time.GetTicksMsec()} nodes={string.Join(',', nodes.Where(IsInstanceValid).Select(n => n.GetInstanceId()))}");
        GetTree().CreateTimer(remaining).Timeout += () =>
        {
            if (_conjureLifetimes.TryGetValue(caster, out var current)
                && current == (presentation, revision)
                && _conjureHands.TryGetValue(caster, out var active) && active == nodes)
                EndConjureHands(caster);
        };
    }

    static long Presentation(JsonElement d) => d.TryGetProperty("presentation", out var p) ? p.GetInt64() : 0;

    void Release(uint caster, JsonElement d)
    {
        long presentation = Presentation(d);
        if (_pendingReleases.Remove((caster, presentation), out var play)) play();
        foreach (var projectile in _projectiles)
            if (projectile.Caster == caster && projectile.Presentation == presentation && projectile.AwaitRelease)
            {
                projectile.AwaitRelease = false;
                projectile.Age = 0;
            }
    }

    void PlayRelease(uint caster, string head, string hand, string ground, string sound)
    {
        if (!Session.Objects.TryGetValue(caster, out var view)) return;
        var nodes = new List<Node3D>();
        Place(nodes, head, view, "headconjure", burst: true);
        Place(nodes, hand, view, "handconjure", burst: true);
        Place(nodes, ground, view, "", burst: true);
        Sound(nodes, sound, view);
        foreach (var n in nodes.Where(n => !n.GetChildren().Any(c => c is AudioStreamPlayer3D)))
            GetTree().CreateTimer(EffectPlayer.OneShotSeconds(n)).Timeout += () => { if (IsInstanceValid(n)) n.QueueFree(); };
    }

    void StopConjure(uint caster)
    {
        _conjureActions.Remove(caster);
        _castPoses.Remove(caster);
        EndConjureHands(caster);
        EndConjureModels(caster);
    }

    void EndConjureHands(uint caster)
    {
        // A capped hand effect may end before a long preparation clip. Its removal
        // must not invalidate later live-Haste updates to that independent action.
        _conjureLifetimes.Remove(caster);
        if (!_conjureHands.Remove(caster, out var nodes)) return;
        if (WorldFx.WorldFxPlayer.Verbose)
            GD.Print($"[conjure-life] stop caster={caster} ticks={Time.GetTicksMsec()} nodes={string.Join(',', nodes.Where(IsInstanceValid).Select(n => n.GetInstanceId()))}");
        // Native prepared self-casts remove the whole hand model at this boundary.
        // Authored keyed markers with two-second particle lives disappear together,
        // without running their fade clip or draining particles. These lists contain
        // only hand models; casting audio and other attachments have separate owners.
        foreach (var node in nodes)
            if (IsInstanceValid(node)) node.QueueFree();
    }

    void EndConjureModels(uint caster)
    {
        if (_conjuring.Remove(caster, out var nodes)) EndNodes(nodes);
    }

    void EndNodes(List<Node3D> nodes)
    {
        foreach (var n in nodes)
        {
            if (!IsInstanceValid(n)) continue;
            Effects.EndEffect(n);
        }
    }

    void Interrupt(uint caster, JsonElement d)
    {
        // Cancelling preparation returns to the waiting stance immediately. Only
        // stop the correlated clip; a newer gesture must survive an old notice.
        if (S(d, "reason") == "cancelled" && _conjureActions.TryGetValue(caster, out var action)
            && Session.Objects.GetValueOrDefault(caster) is CreatureView { Animator: { } animator })
            animator.RetimeAction(action.Action, 0);
        foreach (var key in _pendingReleases.Keys.Where(key => key.Caster == caster).ToArray())
            _pendingReleases.Remove(key);
        StopConjure(caster);
        CancelPendingProjectiles(caster);
        if (S(d, "reason") != "concentration") return;
        // Native damage/concentration failure uses this installed sample, not gui_failspell.
        Sound(null, "sff_spellfail", Session.Objects.GetValueOrDefault(caster));
    }

    void Place(List<Node3D> into, string model, ObjectView? on, string node, bool burst)
    {
        if (model.Length == 0 || Effects.BuildModel(model, burst) is not { } fx) return;
        Effects.Attach(fx, on, node, on == null ? Vector3.Zero : null);
        into.Add(fx);
    }

    void Sound(List<Node3D>? into, string resref, ObjectView? at)
    {
        if (resref.Length == 0 || Session.Content?.Audio.Get(resref) is not { } stream || at == null) return;
        var holder = new Node3D();
        var player = new AudioStreamPlayer3D { Stream = stream, MaxDistance = 40, Bus = OpenWinter.Client.Preferences.AudioBuses.Effects3D };
        holder.AddChild(player);
        at.AddChild(holder);
        ulong started = Time.GetTicksMsec();
        if (WorldFx.WorldFxPlayer.Verbose) GD.Print($"[spell-audio] start {resref} caster={at.ObjectId} length={stream.GetLength():F3}");
        player.Play();
        player.Finished += () =>
        {
            if (WorldFx.WorldFxPlayer.Verbose) GD.Print($"[spell-audio] finished {resref} elapsed={(Time.GetTicksMsec() - started) / 1000.0:F3}");
            if (IsInstanceValid(holder)) holder.QueueFree();
        };
        if (WorldFx.WorldFxPlayer.Verbose)
            holder.TreeExiting += () => GD.Print($"[spell-audio] cleanup {resref} playing={player.Playing} position={player.GetPlaybackPosition():F3} elapsed={(Time.GetTicksMsec() - started) / 1000.0:F3}");
        into?.Add(holder);
    }

}
