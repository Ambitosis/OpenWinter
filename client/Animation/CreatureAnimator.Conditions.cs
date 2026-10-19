using Godot;

namespace OpenWinter.Client.Anim;

/// <summary>
/// Conditions that put a creature on the ground, as the original shows them (parity/visual scenes conditions and
/// coupdegrace):
/// <list type="bullet">
/// <item>asleep: falls forward (kdfnt) and lies face down (the deadfnt pose); woken, it pushes itself up (gutokdf)
/// and stands (gustandf);</item>
/// <item>knocked down: falls back (kdbck) and stays sitting up (kdbckps), then stands (gustandb); knocked down from
/// behind, it falls forwards (kdfnt), kneels (kdfntps) and stands (gustandf).</item>
/// </list>
/// The states live in the death state machine, so death takes over from them (a sleeper killed by a coup de grace
/// stays where it lies).
/// </summary>
public partial class CreatureAnimator
{
    public enum DownKind { None, Asleep, KnockedDown, KnockedForward }

    DownKind _down;
    readonly HashSet<string> _downStates = new();
    double _downStartedAt;
    string _sleepFall = "", _kdFall = "", _kdFrontFall = "";

    void AddDownStates(AnimationNodeStateMachine death)
    {
        string recovered = Pick(_profile.Idle);
        if (!death.HasNode("recovered")) death.AddNode("recovered", new AnimationNodeAnimation { Animation = recovered });
        _sleepFall = First("kdfnt", "ckdfnt", "kdbck", "ckdbck");
        _kdFall = First("kdbck", "ckdbck", "kdfnt", "ckdfnt");
        AddChain(death, ("sleepfall", _sleepFall, false), ("sleep", First("deadfnt", "cdeadfnt", "kdfntps", "ckdfntps", "cdead", "deadbck"), false));
        AddChain(death, ("wake", First("gutokdf", "cgutokdf", "cguptokdf", "gutokdb", "cgutokdb")), ("wakestand", First("gustandf", "cgustandf", "gustandb", "cgustandb")), ("recovered", "", true));
        AddChain(death, ("kdfall", _kdFall, false), ("kd", First("kdbckps", "ckdbckps", "kdfntps", "ckdfntps"), true));
        AddChain(death, ("kdstand", First("gustandb", "cgustandb", "gustandf", "cgustandf")), ("recovered", "", true));
        _kdFrontFall = First("kdfnt", "ckdfnt", "kdbck", "ckdbck");
        AddChain(death, ("kdffall", _kdFrontFall, false), ("kdf", First("kdfntps", "ckdfntps", "kdbckps", "ckdbckps"), true));
        AddChain(death, ("kdfstand", First("gustandf", "cgustandf", "gustandb", "cgustandb")), ("recovered", "", true));
    }

    string First(params string[] clips) => clips.FirstOrDefault(HasClip) ?? "";

    void AddChain(AnimationNodeStateMachine sm, params (string State, string Clip, bool Loop)[] chain)
    {
        string previous = "";
        foreach (var (state, clip, loop) in chain)
        {
            if (state != "recovered")
            {
                if (clip.Length == 0) continue;
                if (!sm.HasNode(state)) sm.AddNode(state, new AnimationNodeAnimation { Animation = clip });
                _library.GetAnimation(clip).LoopMode = loop ? Godot.Animation.LoopModeEnum.Linear : Godot.Animation.LoopModeEnum.None;
                _downStates.Add(state);
            }
            if (previous.Length > 0 && !sm.HasTransition(previous, state))
                sm.AddTransition(previous, state, new AnimationNodeStateMachineTransition
                {
                    AdvanceMode = AnimationNodeStateMachineTransition.AdvanceModeEnum.Auto,
                    SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.AtEnd,
                    XfadeTime = state == "recovered" ? 0.2f : Math.Min(0.25f, Fade(clip)),
                });
            previous = state;
        }
    }

    void AddChain(AnimationNodeStateMachine sm, (string State, string Clip) a, (string State, string Clip) b, (string State, string Clip, bool Loop) c) =>
        AddChain(sm, (a.State, a.Clip, false), (b.State, b.Clip, false), c);

    void AddChain(AnimationNodeStateMachine sm, (string State, string Clip) a, (string State, string Clip, bool Loop) b) =>
        AddChain(sm, (a.State, a.Clip, false), b);

    /// <summary>Puts the creature down for a condition, or lets it get up (none). Death overrides it.</summary>
    public void SetDown(DownKind kind, bool immediate = false)
    {
        if (_down == kind || _dead || !_hasDeath) return;
        var was = _down;
        _down = kind;
        var playback = (AnimationNodeStateMachinePlayback)Get("parameters/death/playback");
        StopAction();
        StopGesture();
        EndBump();
        string start = kind switch
        {
            DownKind.Asleep => immediate || !_downStates.Contains("sleepfall") ? "sleep" : "sleepfall",
            DownKind.KnockedDown => immediate || !_downStates.Contains("kdfall") ? "kd" : "kdfall",
            DownKind.KnockedForward => immediate || !_downStates.Contains("kdffall") ? "kdf" : "kdffall",
            _ => immediate ? "recovered" : was switch
            {
                DownKind.Asleep => _downStates.Contains("wake") ? "wake" : "wakestand",
                DownKind.KnockedForward => "kdfstand",
                _ => "kdstand",
            },
        };
        if (kind != DownKind.None && !_downStates.Contains(start)) { _down = DownKind.None; return; }
        if (kind == DownKind.None && start != "recovered" && !_downStates.Contains(start)) start = "recovered";
        _downStartedAt = _clock;
        playback.Start(start, true);
        _recovering = kind == DownKind.None && start != "recovered";
        Set("parameters/life/transition_request", kind != DownKind.None || _recovering ? "dead" : "alive");
    }

    /// <summary>Whether the creature lies or sits on the ground for a condition (no locomotion meanwhile).</summary>
    public bool IsDown => _down != DownKind.None;

    void UpdateDown()
    {
        if (_down == DownKind.None) return;
        // The fall clips don't loop; move on to the held pose when they end (as death does).
        var playback = (AnimationNodeStateMachinePlayback)Get("parameters/death/playback");
        string node = playback.GetCurrentNode();
        string fall = _down switch { DownKind.Asleep => _sleepFall, DownKind.KnockedForward => _kdFrontFall, _ => _kdFall };
        if (node is "sleepfall" or "kdfall" or "kdffall" && fall.Length > 0 && _clock - _downStartedAt >= _library.GetAnimation(fall).Length)
            playback.Travel(node switch { "sleepfall" => "sleep", "kdffall" => "kdf", _ => "kd" });
    }
}
