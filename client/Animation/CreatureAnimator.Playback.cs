using Godot;

namespace OpenWinter.Client.Anim;

public partial class CreatureAnimator
{
    /// <summary>An in-progress action, independent of the model being replaced.</summary>
    public sealed record ActionPlayback(string Clip, double Position, double Remaining,
        Godot.Animation.LoopModeEnum Loop, string CastLoop, double UntilCastLoop, float MoveGrace, bool Backward = false);

    public ActionPlayback? CaptureAction()
    {
        if (_actionEnd <= _clock) return null;
        string clip = _actionClip.Animation;
        var animation = _library.GetAnimation(clip);
        return new(clip, _clock - _actionStarted, _actionEnd - _clock,
            animation.LoopMode, _castLoop, _castLoopAt - _clock, _actionMoveGrace,
            _actionClip.PlayMode == AnimationNodeAnimation.PlayModeEnum.Backward);
    }

    public void RestoreAction(ActionPlayback? playback)
    {
        if (playback == null || !HasClip(playback.Clip)) return;
        var animation = _library.GetAnimation(playback.Clip);
        animation.LoopMode = playback.Loop;
        _actionClip.Animation = playback.Clip;
        _actionClip.PlayMode = playback.Backward ? AnimationNodeAnimation.PlayModeEnum.Backward : AnimationNodeAnimation.PlayModeEnum.Forward;
        ActionVersion++;
        // An established action must not blend up from idle again after changing clothes/skin.
        _actionShot.FadeInTime = Math.Max(0, (playback.Clip is "drwright" or "drwleft" ? Fade(playback.Clip) : 0.12) - playback.Position);
        _actionShot.FadeOutTime = playback.Clip is "getlow" or "getlowlp" or "drwright" or "drwleft" ? Fade(playback.Clip) : 0.25f;
        double position = playback.Position;
        if (animation.Length > 0 && playback.Loop != Godot.Animation.LoopModeEnum.None)
            position %= animation.Length;
        Set("parameters/action_seek/seek_request", position);
        Set("parameters/action/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
        _actionStarted = _clock - playback.Position;
        _actionEnd = _clock + playback.Remaining;
        _actionMoveGrace = playback.MoveGrace;
        if (playback.CastLoop.Length > 0 && HasClip(playback.CastLoop))
        {
            _castLoop = playback.CastLoop;
            _castLoopAt = _clock + playback.UntilCastLoop;
        }
    }
}
