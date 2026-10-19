using Godot;

namespace OpenWinter.Client.Anim;

public partial class CreatureAnimator
{
    string _castLoop = "";
    double _castLoopAt;

    // The authored intro reaches the pose; the lp clip continues it until commitment.
    // Native grounditemtiming also uses getlow's low pose through the item transfer,
    // rather than recovering immediately after the half-second reach.
    bool TryCastSequence(string clip, float duration)
    {
        if (duration <= 0 || clip is not ("castout" or "ccastout" or "castpoint" or "castup" or "castself" or "castarea" or "getlow" or "sitcross")) return false;
        string loop = clip == "sitcross" ? "sitcrossps" : clip + "lp";
        if (!HasClip(loop)) return false;
        var intro = _library.GetAnimation(clip);
        // Keep full weight through the release. A nonlooping one-shot starts its fade-out
        // before the last frame, briefly dropping the arms before the recovery pose. Ping-pong, not a plain loop:
        // the tree advances the intro before _Process switches to the lp clip, and a plain loop had wrapped to the
        // intro's first frame by then, showing one frame of the standing pose at every switch (dropchain captures).
        intro.LoopMode = Godot.Animation.LoopModeEnum.Pingpong;
        PlayAction(clip);
        _actionEnd = _clock + duration;
        if (duration > intro.Length)
        {
            _castLoop = loop;
            _castLoopAt = _clock + intro.Length;
        }
        return true;
    }

    /// <summary>How long a chained reach holds the low loop past its own time, waiting for the next reach's notice.</summary>
    public const float ReachChainHold = 0.5f;

    /// <summary>
    /// The low reach of an item put-down or pick-up (getlow into getlowlp) for <paramref name="duration"/> seconds.
    /// The original stays low through consecutive queued reaches and stands once after the last (parity/visual scene
    /// dropchain), so a reach that arrives while the creature is still low continues the loop rather than bending
    /// again, and a <paramref name="chained"/> one (the server has another queued straight after it) holds the loop
    /// <see cref="ReachChainHold"/> longer so that the next notice, a server tick and the network later, finds her low.
    /// </summary>
    public void PlayReach(float duration, bool chained)
    {
        if (_dead) return;
        string clip = Resolve("getlow");
        double hold = chained ? ReachChainHold : 0;
        if (duration > 0 && _actionEnd > _clock && (_actionClip.Animation == clip || _actionClip.Animation == clip + "lp"))
        {
            _actionEnd = _clock + duration + hold;
            return;
        }
        PlayAction("getlow", duration);
        if (_actionEnd > _clock && _actionClip.Animation == clip) _actionEnd += hold;
    }

    /// <summary>The server cancelled the reach: stand from the low pose now (nothing else is cut short).</summary>
    public void EndReach()
    {
        string clip = Resolve("getlow");
        if (_actionEnd > _clock && (_actionClip.Animation == clip || _actionClip.Animation == clip + "lp")) StopAction();
    }

    /// <summary>Uses the spell's release boundary for the measured creature out-cast recovery.</summary>
    public void SetCreatureCastRelease(ulong version, float seconds)
    {
        if (version != ActionVersion || _actionEnd <= _clock || !float.IsFinite(seconds) || seconds < 0
            || _actionClip.Animation != "ccastout" || !HasClip("ccastoutlp")) return;
        // Native switches to ccastoutlp near release even when the installed or
        // publicly replaced intro is longer. Do not replay or rescale the intro.
        _castLoop = "ccastoutlp";
        _castLoopAt = _clock + seconds;
    }

    void ResetCastSequence() { _castLoop = ""; _castLoopAt = 0; }

    void UpdateCastSequence()
    {
        if (_castLoop.Length == 0 || _clock < _castLoopAt) return;
        _library.GetAnimation(_castLoop).LoopMode = Godot.Animation.LoopModeEnum.Linear;
        _actionClip.Animation = _castLoop;
        if (_castLoop is "getlowlp" or "sitcrossps")
        {
            // The loop starts at the intro's final pose; do not fade up from idle again.
            _actionShot.FadeInTime = 0;
            _actionShot.FadeOutTime = Fade(_castLoop);
        }
        _actionStarted = _clock;
        Set("parameters/action_seek/seek_request", 0.0);
        Set("parameters/action/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
        ResetCastSequence();
    }
}
