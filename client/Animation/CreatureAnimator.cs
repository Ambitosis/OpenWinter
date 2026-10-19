using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Anim;

/// <summary>Clips and speeds that drive a CreatureAnimator.</summary>
public sealed class AnimationProfile
{
    public string Idle = "pause1", Walk = "walk", Run = "run";
    public string CombatIdle = "", CombatWalk = "", ItemReady = "";
    /// <summary>Ground speed (m/s) at which the walk/run clips play at 1x.</summary>
    public float WalkSpeed = 1.8f, RunSpeed = 3.5f;
    public string UpperBodyRoot = "torso_g";
    public Dictionary<string, float> Transitions = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Data-driven animation graph: a stance state machine whose states are
/// speed-driven locomotion blend spaces, an arm pose held over them, plus an
/// upper-body gesture layer and a full-body action layer.
/// <code>
///   base (state machine: peace ⇄ combat; each a blend space idle → walk → run, rate-matched to ground speed)
///   bump    (directional cwalk step, retaining world facing)
///   arm     (a held pose on one arm's subtree: torchl while a torch is in the left hand)
///   gesture (one-shot, upper body only)
///   action  (one-shot, full body)
/// </code>
/// </summary>
public partial class CreatureAnimator : AnimationTree
{
    public const string Peace = "peace", Combat = "combat", ItemReady = "item-ready";

    /// <summary>Ground speed in m/s, set by whatever moves the creature.</summary>
    public float GroundSpeed;

    AnimationProfile _profile = new();
    AnimationNodeAnimation _gestureClip = null!, _actionClip = null!, _armClip = null!;
    AnimationNodeAnimation _bumpClip = null!;
    AnimationNodeTransition _drive = null!;
    MovementMode _requestedDrive, _shownDrive;
    readonly Dictionary<MovementMode, string> _driveClips = new();
    float _turnStepRemaining;
    bool _hasCombatWalk;
    float _combatWalkWeight, _baseSpeed;
    AnimationNodeOneShot _bumpShot = null!;
    AnimationNodeOneShot _actionShot = null!;
    string _bumpAnimation = "";
    AnimationNodeBlend2 _arm = null!;
    Skeleton3D _skeleton = null!;
    string _armPose = "";
    float _armWeight, _armFade = 0.25f;
    AnimationLibrary _library = null!;
    string _stance = Peace;
    readonly List<string> _stances = new();

    public void Setup(AnimationLibrary library, Skeleton3D skeleton, AnimationProfile profile)
    {
        _profile = profile;
        _library = library;
        _skeleton = skeleton;
        AddAnimationLibrary("", library);
        foreach (var clip in new[] { profile.Idle, profile.Walk, profile.Run, profile.CombatIdle, profile.CombatWalk, profile.ItemReady })
            if (library.HasAnimation(clip)) library.GetAnimation(clip).LoopMode = Godot.Animation.LoopModeEnum.Linear;

        var sm = new AnimationNodeStateMachine();
        sm.AddNode(Peace, Locomotion(Pick(profile.Idle), Pick(profile.Walk), Pick(profile.Run)), new Vector2(200, 0));
        _stances.Add(Peace);
        var start = new AnimationNodeStateMachineTransition { AdvanceMode = AnimationNodeStateMachineTransition.AdvanceModeEnum.Auto };
        sm.AddTransition("Start", Peace, start);
        if (library.HasAnimation(profile.CombatIdle))
        {
            var cwalk = library.HasAnimation(profile.CombatWalk) ? profile.CombatWalk : Pick(profile.Walk);
            sm.AddNode(Combat, Locomotion(profile.CombatIdle, cwalk, Pick(profile.Run)), new Vector2(400, 0));
            sm.AddTransition(Peace, Combat, Crossfade(Fade(profile.CombatIdle)));
            sm.AddTransition(Combat, Peace, Crossfade(Fade(profile.Idle)));
            _stances.Add(Combat);
        }

        if (library.HasAnimation(profile.ItemReady))
        {
            sm.AddNode(ItemReady, Locomotion(profile.ItemReady, Pick(profile.Walk), Pick(profile.Run)), new Vector2(600, 0));
            foreach (string stance in _stances)
            {
                sm.AddTransition(stance, ItemReady, Crossfade(Fade(profile.ItemReady)));
                sm.AddTransition(ItemReady, stance, Crossfade(Fade(profile.Idle)));
            }
            _stances.Add(ItemReady);
        }

        var gesture = new AnimationNodeOneShot { FadeInTime = 0.25f, FadeOutTime = 0.35f, FilterEnabled = true };
        int upper = skeleton.FindBone(profile.UpperBodyRoot);
        if (upper < 0) upper = Rendering.ModelBuilder.FindBone(skeleton, profile.UpperBodyRoot);
        if (upper >= 0)
            foreach (int b in Subtree(skeleton, upper))
                gesture.SetFilterPath(new NodePath($"{skeleton.Name}:{skeleton.GetBoneName(b)}"), true);
        else gesture.FilterEnabled = false;
        var action = new AnimationNodeOneShot { FadeInTime = 0.12f, FadeOutTime = 0.25f };
        _actionShot = action;

        _gestureClip = new AnimationNodeAnimation { Animation = Pick(profile.Idle) };
        _actionClip = new AnimationNodeAnimation { Animation = Pick(profile.Idle) };
        _armClip = new AnimationNodeAnimation { Animation = Pick(profile.Idle) };
        _bumpClip = new AnimationNodeAnimation { Animation = Pick(profile.Idle) };
        _bumpShot = new AnimationNodeOneShot { FadeInTime = 0.1f, FadeOutTime = 0.1f };
        _arm = new AnimationNodeBlend2 { FilterEnabled = true };
        var root = new AnimationNodeBlendTree();
        root.AddNode("base", sm, new Vector2(0, 0));
        // Keep separate playback positions for outgoing and incoming clips. Replacing the
        // animation on one full-weight node cuts the pose immediately on every key change.
        var modes = Enum.GetValues<MovementMode>();
        _drive = new AnimationNodeTransition { InputCount = modes.Length, XfadeTime = Fade(profile.Idle) };
        root.AddNode("drive", _drive);
        foreach (var mode in modes)
        {
            int index = (int)mode;
            _drive.SetInputName(index, mode.ToString());
            // The idle stance machine must retain combat/peace when returning from movement.
            _drive.SetInputReset(index, mode != MovementMode.Stand);
            if (mode == MovementMode.Stand) { root.ConnectNode("drive", index, "base"); continue; }
            string clip = DriveClip(mode);
            if (!HasClip(clip)) clip = Pick(mode is MovementMode.TurnLeft or MovementMode.TurnRight ? profile.Idle : profile.Walk);
            _driveClips[mode] = clip;
            _library.GetAnimation(clip).LoopMode = Godot.Animation.LoopModeEnum.Linear;
            var motion = new AnimationNodeBlendTree();
            motion.AddNode("clip", new AnimationNodeAnimation { Animation = clip });
            motion.AddNode("rate", new AnimationNodeTimeScale());
            if (mode == MovementMode.Walk && HasClip(profile.CombatWalk))
            {
                _hasCombatWalk = true;
                motion.AddNode("combat", new AnimationNodeAnimation { Animation = profile.CombatWalk });
                motion.AddNode("stance", new AnimationNodeBlend2());
                motion.ConnectNode("stance", 0, "clip");
                motion.ConnectNode("stance", 1, "combat");
                motion.ConnectNode("rate", 0, "stance");
            }
            else motion.ConnectNode("rate", 0, "clip");
            motion.ConnectNode("output", 0, "rate");
            root.AddNode("drive_" + mode, motion);
            root.ConnectNode("drive", index, "drive_" + mode);
        }
        root.AddNode("bump_clip", _bumpClip, new Vector2(0, -200));
        root.AddNode("bump", _bumpShot, new Vector2(100, 0));
        root.AddNode("arm_clip", _armClip, new Vector2(0, 200));
        root.AddNode("arm", _arm, new Vector2(150, 0));
        root.AddNode("gesture_clip", _gestureClip, new Vector2(0, 200));
        root.AddNode("gesture", gesture, new Vector2(250, 0));
        root.AddNode("action_clip", _actionClip, new Vector2(250, 200));
        root.AddNode("action_seek", new AnimationNodeTimeSeek());
        root.AddNode("action", action, new Vector2(500, 0));
        root.ConnectNode("bump", 0, "drive");
        root.ConnectNode("bump", 1, "bump_clip");
        root.ConnectNode("arm", 0, "bump");
        root.ConnectNode("arm", 1, "arm_clip");
        root.ConnectNode("gesture", 0, "arm");
        root.ConnectNode("gesture", 1, "gesture_clip");
        root.ConnectNode("action", 0, "gesture");
        root.ConnectNode("action_seek", 0, "action_clip");
        root.ConnectNode("action", 1, "action_seek");
        AddLifeLayer(root);
        TreeRoot = root;
        Active = true;
    }

    string Pick(string clip) => _library.HasAnimation(clip) ? clip : _library.GetAnimationList().FirstOrDefault().ToString() ?? clip;

    public bool HasClip(string clip) => _library != null && _library.HasAnimation(clip);

    /// <summary>Capture diagnostics for detecting unintended locomotion restarts.</summary>
    public MovementMode DriveMode => _shownDrive;
    public int DriveTransitions { get; private set; }

    string DriveClip(MovementMode mode) => mode switch
    {
        MovementMode.TurnLeft or MovementMode.TurnRight => "pauseturn",
        MovementMode.StrafeLeft => "pausewalkl",
        MovementMode.StrafeRight => "pausewalkr",
        MovementMode.Backward or MovementMode.Walk or MovementMode.MouseWalk => _profile.Walk,
        MovementMode.Run or MovementMode.MouseRun => _profile.Run,
        MovementMode.WalkForwardLeft => "pausewalkfl",
        MovementMode.WalkForwardRight => "pausewalkfr",
        MovementMode.RunForwardLeft => "runfl",
        MovementMode.RunForwardRight => "runfr",
        _ => _profile.Idle,
    };

    /// <summary>Released movement finishes on the server; a stationary turn finishes its footwork here.</summary>
    public void SetDrive(MovementMode mode, bool finishTurn = true)
    {
        _requestedDrive = mode;
        if (!finishTurn) _turnStepRemaining = 0;
    }

    void UpdateDrive(float dt)
    {
        if (_hasCombatWalk)
        {
            _combatWalkWeight = Mathf.MoveToward(_combatWalkWeight, _stance == Combat ? 1f : 0f, dt / Fade(_profile.CombatWalk));
            Set("parameters/drive_Walk/stance/blend_amount", _combatWalkWeight);
        }
        bool turning = _shownDrive is MovementMode.TurnLeft or MovementMode.TurnRight;
        _turnStepRemaining = Math.Max(0, _turnStepRemaining - dt);
        var mode = _requestedDrive;
        if (mode == MovementMode.Stand && turning && _turnStepRemaining > 0) mode = _shownDrive;
        // A destination move brakes while still reporting Walk/Run. Below the clip's
        // natural speed, let the existing run/walk/idle blend take over rather than
        // stretching a full-weight running pose into slow motion until arrival.
        if (mode is MovementMode.Run or MovementMode.Walk)
        {
            float nativeSpeed = mode == MovementMode.Run ? _profile.RunSpeed : _profile.WalkSpeed;
            // Distance-derived speed fluctuates around the native rate while steering.
            // Re-entering this reset-on-entry clip on each crossing skips the stride.
            // Tolerate rounding at entry, and require a real slowdown before leaving it.
            float threshold = nativeSpeed * (_shownDrive == mode ? 0.99f : 0.999f);
            if (GroundSpeed < threshold) mode = MovementMode.Stand;
        }
        if (mode != _shownDrive)
        {
            // Native tap endings settle over the movement clip's short transition.
            // pause1's general-purpose fade is 0.7 s on a_ba and adds extra footwork here.
            string clip = mode == MovementMode.Stand ? _driveClips[_shownDrive] : _driveClips[mode];
            bool stopping = mode == MovementMode.Stand && _requestedDrive == MovementMode.Stand;
            if (stopping)
            {
                // Fade the completed step's pose directly to idle. Continuing the outgoing
                // loop starts another footstep, and a stale base speed blends in forward walk
                // even when the step that just finished was backwards or sideways.
                Set($"parameters/drive_{_shownDrive}/rate/scale", 0f);
                _baseSpeed = 0;
            }
            DriveTransitions++;
            _shownDrive = mode;
            // tapfinish: the completed step settles in roughly 0.1 s; a 0.2 s
            // movement fade still leaves the client a sample behind the original.
            _drive.XfadeTime = stopping ? Math.Min(0.1f, Fade(clip)) : Fade(clip);
            Set("parameters/drive/transition_request", mode.ToString());
            _turnStepRemaining = mode is MovementMode.TurnLeft or MovementMode.TurnRight
                ? (float)_library.GetAnimation(clip).Length : 0;
        }
        else if (turning && _requestedDrive != MovementMode.Stand && _turnStepRemaining <= 0)
            _turnStepRemaining = (float)_library.GetAnimation(_driveClips[mode]).Length;
        if (mode == MovementMode.Stand) return;
        float rate = mode switch
        {
            MovementMode.Backward => -GroundSpeed / Math.Max(0.01f, _profile.WalkSpeed),
            MovementMode.Walk or MovementMode.MouseWalk => GroundSpeed / Math.Max(0.01f, _profile.WalkSpeed),
            MovementMode.Run or MovementMode.MouseRun => GroundSpeed / Math.Max(0.01f, _profile.RunSpeed),
            _ => 1f,
        };
        Set($"parameters/drive_{mode}/rate/scale", rate);
    }

    /// <summary>Measured directional cwalk step underneath the held torch/arm pose.</summary>
    public float BeginBump(string clip)
    {
        if (!HasClip(clip)) clip = HasClip("cwalk") ? "cwalk" : Pick(_profile.Walk);
        if (!HasClip(clip)) return 0.5f;
        // Continue the stride during chained pushes instead of restarting it every network event.
        bool continuing = _bumpAnimation == clip && (bool)Get("parameters/bump/active");
        _bumpAnimation = clip;
        _bumpClip.Animation = clip;
        _library.GetAnimation(clip).LoopMode = Godot.Animation.LoopModeEnum.Linear;
        if (!continuing) Set("parameters/bump/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
        return (float)Math.Max(0.01, _library.GetAnimation(clip).Length);
    }

    public void EndBump()
    {
        _bumpAnimation = "";
        if ((bool)Get("parameters/bump/active")) Set("parameters/bump/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
    }

    /// <summary>
    /// The clip to play for an animation name: the name itself, or the creature-set variant
    /// ("c" prefix: cdamages, ccloseh, cdead) that non-humanoid models use, or the same swing in the other set
    /// (<see cref="OpenWinter.Game.Combat.WeaponStances.SwingAlias"/>: nwslashl as ca1slashl) when the model lacks both.
    /// </summary>
    public string Resolve(string clip)
    {
        if (HasClip(clip)) return clip;
        if (HasClip("c" + clip)) return "c" + clip;
        return OpenWinter.Game.Combat.WeaponStances.SwingAlias(clip) is { } alias && HasClip(alias) ? alias : clip;
    }

    public string Stance
    {
        get => _stance;
        set
        {
            if (value == _stance || !_stances.Contains(value)) return;
            _stance = value;
            ((AnimationNodeStateMachinePlayback)Get("parameters/base/playback")).Travel(value);
        }
    }

    /// <summary>
    /// Holds a clip's pose on the subtree under <paramref name="root"/> (the animation's own root node, e.g. lbicep_g)
    /// over every stance and speed, fading in and out over <paramref name="fade"/> seconds; "" lets the arm go.
    /// The original raises a torch held in the left hand this way, standing, walking and running alike
    /// (a_ba_non_combat's torchl: rooted at lbicep_g; parity/visual, scenes torch and torchwalk).
    /// </summary>
    public void SetArmPose(string clip, string root, float fade = 0.25f)
    {
        if (clip.Length > 0 && !HasClip(clip)) clip = "";
        if (clip == _armPose) return;
        _armFade = Math.Max(0.01f, fade);
        if (clip.Length == 0) { _armPose = ""; return; }
        int top = _skeleton.FindBone(root);
        if (top < 0) top = Rendering.ModelBuilder.FindBone(_skeleton, root);
        if (top < 0) return;
        _armPose = clip;
        _library.GetAnimation(clip).LoopMode = Godot.Animation.LoopModeEnum.Linear;
        _armClip.Animation = clip;
        for (int b = 0; b < _skeleton.GetBoneCount(); b++)
            _arm.SetFilterPath(new NodePath($"{_skeleton.Name}:{_skeleton.GetBoneName(b)}"), false);
        foreach (int b in Subtree(_skeleton, top))
            _arm.SetFilterPath(new NodePath($"{_skeleton.Name}:{_skeleton.GetBoneName(b)}"), true);
    }

    /// <summary>The held arm pose ("" for none).</summary>
    public string ArmPose => _armPose;

    /// <summary>
    /// Plays a clip on the upper body only; the legs keep following locomotion. A looping clip (the talk animations)
    /// repeats for <paramref name="duration"/> seconds, or plays one cycle when it's 0; asking for the plain idle
    /// (<see cref="PlayAction"/>) ends it. A one-off gesture played over a looping one returns to it afterwards,
    /// as a speaker's talk animation resumes after a greeting in the original's conversations.
    /// </summary>
    public void PlayGesture(string clip, float duration = 0)
    {
        if (_dead) return;
        clip = Resolve(clip);
        if (!HasClip(clip)) return;
        if (duration > 0)
        {
            _library.GetAnimation(clip).LoopMode = Godot.Animation.LoopModeEnum.Linear;
            _loopGesture = clip;
            _loopGestureEnd = _clock + duration;
        }
        FireGesture(clip, duration > 0 ? duration : Math.Max(0.1f, _library.GetAnimation(clip).Length));
    }

    void FireGesture(string clip, double seconds)
    {
        _gestureClip.Animation = clip;
        Set("parameters/gesture/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
        // Like actions, every gesture gets an end time: the one-shot node never ends a looping clip by itself.
        _gestureEnd = _clock + seconds;
    }

    /// <summary>Fades out the current gesture, a looping one included.</summary>
    public void StopGesture()
    {
        _loopGesture = "";
        EndGesture();
    }

    void EndGesture()
    {
        _gestureEnd = 0;
        if (_loopGesture.Length > 0 && _clock < _loopGestureEnd) { FireGesture(_loopGesture, _loopGestureEnd - _clock); return; }
        _loopGesture = "";
        if ((bool)Get("parameters/gesture/active")) Set("parameters/gesture/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
    }

    string _loopGesture = "";
    double _loopGestureEnd;

    /// <summary>
    /// Plays a clip on the whole body, fading back to locomotion afterwards. A looping clip (sitting,
    /// meditating, resting) holds for <paramref name="duration"/> seconds, or one cycle when it's 0. Asking for
    /// the plain idle ends whatever action is playing, and so does moving (the server's movement replaced it).
    /// </summary>
    public void PlayAction(string clip, float duration = 0)
    {
        if (_dead) return;
        clip = Resolve(clip);
        if (!HasClip(clip)) return;
        ResetCastSequence();
        if (clip == Pick(_profile.Idle) || clip == _profile.CombatIdle) { StopAction(recoverSitting: true); StopGesture(); return; }
        if (TryCastSequence(clip, duration)) return;
        // A duration means a looping animation (the ANIMATION_LOOPING_* ones): it repeats until its time is up.
        if (duration > 0) _library.GetAnimation(clip).LoopMode = Godot.Animation.LoopModeEnum.Linear;
        _actionClip.Animation = clip;
        _actionClip.PlayMode = AnimationNodeAnimation.PlayModeEnum.Forward;
        ActionVersion++;
        _actionStarted = _clock;
        _actionShot.FadeInTime = clip is "drwright" or "drwleft" ? Fade(clip) : 0.12f;
        _actionShot.FadeOutTime = clip is "getlow" or "getlowlp" or "drwright" or "drwleft" ? Fade(clip) : 0.25f;
        Set("parameters/action_seek/seek_request", 0.0);
        Set("parameters/action/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
        // The one-shot node never ends a looping clip by itself, so every action gets an end time.
        _actionEnd = _clock + (duration > 0 ? duration : Math.Max(0.1f, _library.GetAnimation(clip).Length));
        _actionMoveGrace = 0.3f;
    }

    /// <summary>Fades out the current whole-body action, back to locomotion.</summary>
    public void StopAction(bool recoverSitting = false)
    {
        // The server's idle acknowledgement may arrive just after the local rest
        // deadline already began the rise. Do not cut that rise off halfway.
        if (recoverSitting && !_dead && _actionEnd > _clock && _actionClip.Animation == "sitcross"
            && _actionClip.PlayMode == AnimationNodeAnimation.PlayModeEnum.Backward) return;
        if (recoverSitting && TryStandFromSitting()) return;
        ActionVersion++;
        ResetCastSequence();
        _actionEnd = 0;
        if (IsActionPlaying) Set("parameters/action/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
    }

    public bool IsActionPlaying => (bool)Get("parameters/action/active");

    /// <summary>Identifies the current action so delayed timing changes cannot affect a later clip.</summary>
    public ulong ActionVersion { get; private set; }

    public void RetimeAction(ulong version, float remaining)
    {
        // StopAction (including death) invalidates the generation and clears the deadline.
        if (version != ActionVersion || _actionEnd <= 0 || !float.IsFinite(remaining)) return;
        if (remaining <= 0) { StopAction(); return; }
        _actionEnd = _clock + remaining;
    }

    double _clock, _actionEnd, _gestureEnd, _actionStarted;
    /// <summary>A short time after an action starts in which movement doesn't end it (the creature is still settling).</summary>
    float _actionMoveGrace;

    public override void _Process(double delta)
    {
        if (TreeRoot == null || _effectFrozen) return;
        _clock += delta;
        UpdateLife();
        UpdateCastSequence();
        UpdateDrive((float)delta);
        _actionMoveGrace -= (float)delta;
        if (_actionEnd > 0 && (_clock >= _actionEnd || GroundSpeed > 0.3f && _actionMoveGrace <= 0))
            StopAction(recoverSitting: GroundSpeed <= 0.3f);
        // StopAction fades out only a one-shot the graph is already playing. One fired in the same frame (a model
        // rebuilt as worn armour comes off at the end of the drop bend restores getlowlp with milliseconds left)
        // starts after the stop, and a looping clip would then play on with no deadline: end it here.
        if (_actionEnd <= 0 && (bool)Get("parameters/action/internal_active"))
            Set("parameters/action/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
        if (_gestureEnd > 0 && _clock >= _gestureEnd) EndGesture();
        float armTarget = _armPose.Length > 0 ? 1f : 0f;
        if (_armWeight != armTarget)
        {
            _armWeight = Mathf.MoveToward(_armWeight, armTarget, (float)delta / _armFade);
            Set("parameters/arm/blend_amount", _armWeight);
        }
        float walk = _profile.WalkSpeed, run = _profile.RunSpeed;
        // Follow gradual arrival braking directly; soften abrupt keyboard stops over
        // the authored fade when a slow creature is already using the base speed blend.
        float blendRate = Math.Max(run, Math.Max(_baseSpeed, GroundSpeed)) / Fade(_profile.Idle);
        _baseSpeed = Mathf.MoveToward(_baseSpeed, GroundSpeed, (float)delta * blendRate);
        float s = Math.Clamp(_baseSpeed, 0, run * 1.6f);
        // Blend position is ground speed and clips sit at their native speeds,
        // so the blended stride roughly matches; outside them, scale the rate
        // so feet don't slide.
        float native = s >= run ? run : s > walk ? walk + (run - walk) * (s - walk) / Math.Max(0.01f, run - walk) : walk;
        float rate = s < 0.05f ? 1f : Math.Clamp(s / native, 0.6f, 1.6f);
        foreach (var stance in _stances)
        {
            Set($"parameters/base/{stance}/move/blend_position", s);
            Set($"parameters/base/{stance}/rate/scale", rate);
        }
    }

    AnimationNodeBlendTree Locomotion(string idle, string walk, string run)
    {
        var space = new AnimationNodeBlendSpace1D { MinSpace = 0, MaxSpace = _profile.RunSpeed * 1.6f, Sync = true };
        space.AddBlendPoint(new AnimationNodeAnimation { Animation = idle }, 0, -1, "idle");
        space.AddBlendPoint(new AnimationNodeAnimation { Animation = walk }, _profile.WalkSpeed, -1, "walk");
        space.AddBlendPoint(new AnimationNodeAnimation { Animation = run }, _profile.RunSpeed, -1, "run");
        var tree = new AnimationNodeBlendTree();
        tree.AddNode("move", space, new Vector2(0, 0));
        tree.AddNode("rate", new AnimationNodeTimeScale(), new Vector2(200, 0));
        tree.ConnectNode("rate", 0, "move");
        tree.ConnectNode("output", 0, "rate");
        return tree;
    }

    float Fade(string clip) => _profile.Transitions.TryGetValue(clip, out var t) && t > 0 ? t : 0.3f;

    static AnimationNodeStateMachineTransition Crossfade(float seconds) => new()
    {
        XfadeTime = seconds,
        SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate,
    };

    static IEnumerable<int> Subtree(Skeleton3D skeleton, int bone)
    {
        var queue = new Queue<int>();
        queue.Enqueue(bone);
        while (queue.Count > 0)
        {
            int b = queue.Dequeue();
            yield return b;
            foreach (int c in skeleton.GetBoneChildren(b)) queue.Enqueue(c);
        }
    }
}
