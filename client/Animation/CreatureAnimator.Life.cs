using Godot;

namespace OpenWinter.Client.Anim;

public partial class CreatureAnimator
{
    bool _dead, _hasDeath, _recovering;
    double _deathStartedAt;
    string _fallClip = "", _corpseClip = "", _recoverState = "";

    // Death is a persistent full-body state, not a one-shot that fades back to standing.
    // Native deathclips identifies kdbck -> deadbck for humanoids; creature sets use c-prefixed clips.
    void AddLifeLayer(AnimationNodeBlendTree root)
    {
        _fallClip = new[] { "kdbck", "ckdbck", "kdfnt", "ckdfnt" }.FirstOrDefault(HasClip) ?? "";
        _corpseClip = new[] { "deadbck", "cdead", "deadfnt", "dead" }.FirstOrDefault(HasClip) ?? _fallClip;
        _hasDeath = _corpseClip.Length > 0;
        if (!_hasDeath) { root.ConnectNode("output", 0, "action"); return; }
        var death = new AnimationNodeStateMachine();
        death.AddNode("corpse", new AnimationNodeAnimation { Animation = _corpseClip });
        if (_fallClip.Length > 0)
        {
            death.AddNode("fall", new AnimationNodeAnimation { Animation = _fallClip });
            death.AddTransition("fall", "corpse", new AnimationNodeStateMachineTransition
            {
                AdvanceMode = AnimationNodeStateMachineTransition.AdvanceModeEnum.Enabled,
                SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate,
                XfadeTime = Fade(_corpseClip),
            });
        }
        // Native resurrectclips identifies gutokdb -> gustandb -> idle (both stages, in order).
        string fromCorpse = new[] { "gutokdb", "cguptokdb", "cgutokdb", "gutokdf", "cgutokdf" }.FirstOrDefault(HasClip) ?? "";
        string stand = new[] { "gustandb", "cgustandb", "gustandf", "cgustandf" }.FirstOrDefault(HasClip) ?? "";
        string previous = "";
        foreach (var (name, clip) in new[] { ("recover", fromCorpse), ("stand", stand), ("recovered", Pick(_profile.Idle)) })
        {
            if (clip.Length == 0) continue;
            death.AddNode(name, new AnimationNodeAnimation { Animation = clip });
            if (name != "recovered")
            {
                _library.GetAnimation(clip).LoopMode = Godot.Animation.LoopModeEnum.None;
                if (_recoverState.Length == 0) _recoverState = name;
            }
            if (previous.Length > 0) death.AddTransition(previous, name, new AnimationNodeStateMachineTransition
            {
                AdvanceMode = AnimationNodeStateMachineTransition.AdvanceModeEnum.Auto,
                SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.AtEnd,
            });
            previous = name;
        }
        AddDownStates(death);
        var life = new AnimationNodeTransition { InputCount = 2, XfadeTime = 0.1f };
        life.SetInputName(0, "alive");
        life.SetInputName(1, "dead");
        root.AddNode("death", death);
        root.AddNode("life", life);
        root.ConnectNode("life", 0, "action");
        root.ConnectNode("life", 1, "death");
        root.ConnectNode("output", 0, "life");
    }

    /// <summary>Apply authoritative death, including an already-dead spawn or model rebuild.</summary>
    public void SetDead(bool dead, bool immediate = false)
    {
        if (_dead == dead) return;
        _dead = dead;
        _recovering = false;
        StopAction();
        StopGesture();
        EndBump();
        if (!_hasDeath) return;
        if (dead && _down == DownKind.Asleep && _downStates.Contains("sleep"))
        {
            // A sleeper killed where it lies (a coup de grace) stays lying.
            _deathStartedAt = _clock - (_fallClip.Length > 0 ? _library.GetAnimation(_fallClip).Length : 0);
            _down = DownKind.None;
        }
        else if (dead)
        {
            _down = DownKind.None;
            _deathStartedAt = immediate && _fallClip.Length > 0 ? _clock - _library.GetAnimation(_fallClip).Length : _clock;
            if (_fallClip.Length > 0) _library.GetAnimation(_fallClip).LoopMode = Godot.Animation.LoopModeEnum.None;
            _library.GetAnimation(_corpseClip).LoopMode = Godot.Animation.LoopModeEnum.None;
            ((AnimationNodeStateMachinePlayback)Get("parameters/death/playback")).Start(
                immediate || _fallClip.Length == 0 ? "corpse" : "fall", true);
        }
        else if (!immediate && _recoverState.Length > 0)
        {
            _recovering = true;
            ((AnimationNodeStateMachinePlayback)Get("parameters/death/playback")).Start(_recoverState, true);
        }
        Set("parameters/life/transition_request", dead || _recovering ? "dead" : "alive");
    }
    public double DeathFallRemaining => !_dead || _fallClip.Length == 0 ? 0
        : Math.Max(0, _library.GetAnimation(_fallClip).Length - (_clock - _deathStartedAt));

    void UpdateLife()
    {
        if (_dead && _hasDeath && _fallClip.Length > 0 && DeathFallRemaining <= 0)
        {
            var playback = (AnimationNodeStateMachinePlayback)Get("parameters/death/playback");
            if (playback.GetCurrentNode() == "fall") playback.Travel("corpse");
        }
        UpdateDown();
        if (!_recovering || ((AnimationNodeStateMachinePlayback)Get("parameters/death/playback")).GetCurrentNode() != "recovered") return;
        _recovering = false;
        Set("parameters/life/transition_request", "alive");
    }
}
