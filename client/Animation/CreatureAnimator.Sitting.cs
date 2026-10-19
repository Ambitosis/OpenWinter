using Godot;

namespace OpenWinter.Client.Anim;

public partial class CreatureAnimator
{
    /// <summary>Rest ends on the server acknowledgement, including when simulation is paused.</summary>
    public void HoldSittingUntilStopped()
    {
        if (_actionEnd > _clock && _actionClip.PlayMode == AnimationNodeAnimation.PlayModeEnum.Forward
            && (string)_actionClip.Animation is "sitcross" or "sitcrossps") _actionEnd = _clock + float.MaxValue;
    }

    // Native restplayercontrols isolates sitcross on both entry and exit, and
    // sitcrossps while seated. Recover with the authored entry in reverse.
    bool TryStandFromSitting()
    {
        if (_dead || _actionEnd <= 0 || !HasClip("sitcross")
            || _actionClip.PlayMode != AnimationNodeAnimation.PlayModeEnum.Forward
            || (string)_actionClip.Animation is not ("sitcross" or "sitcrossps")) return false;
        var intro = _library.GetAnimation("sitcross");
        double entered = _actionClip.Animation == "sitcross" ? Math.Min(intro.Length, _clock - _actionStarted) : intro.Length;
        PlayAction("sitcross");
        intro.LoopMode = Godot.Animation.LoopModeEnum.None;
        _actionClip.PlayMode = AnimationNodeAnimation.PlayModeEnum.Backward;
        // A cancellation during entry returns from the current point, without
        // first snapping all the way down into the seated pose.
        double offset = Math.Max(0, intro.Length - entered);
        Set("parameters/action_seek/seek_request", offset);
        _actionStarted = _clock - offset;
        _actionEnd = _clock + Math.Max(.01, entered);
        return true;
    }
}
