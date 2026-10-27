using Godot;

namespace OpenWinter.Client.Anim;

public partial class CreatureAnimator
{
    /// <summary>The closing clip of the custom animation playing (customNend), played when it ends; "" for none.</summary>
    string _customEnd = "";

    /// <summary>
    /// ANIMATION_LOOPING_CUSTOMn ("customN") on a model with a customNlp clip: customNstart (when the model has one)
    /// into customNlp, repeated for <paramref name="duration"/> seconds, then customNend. Visual scene camanim: CUSTOM3
    /// on the stock humanoid plays its custom3 clips, and a module's hak supplies others (a climb as custom6).
    /// </summary>
    bool TryCustomSequence(string clip, float duration)
    {
        if (!IsCustom(clip) || !HasClip(clip + "lp")) return false;
        string start = clip + "start", loop = clip + "lp", end = clip + "end";
        _library.GetAnimation(loop).LoopMode = Godot.Animation.LoopModeEnum.Linear;
        if (HasClip(start))
        {
            var intro = _library.GetAnimation(start);
            // Held at its last pose until the loop takes over, as a cast's intro is.
            intro.LoopMode = Godot.Animation.LoopModeEnum.Pingpong;
            PlayAction(start);
            _actionEnd = _clock + (duration > 0 ? Math.Max(duration, intro.Length) : intro.Length + _library.GetAnimation(loop).Length);
            _castLoop = loop;
            _castLoopAt = _clock + intro.Length;
        }
        else PlayAction(loop, duration > 0 ? duration : (float)_library.GetAnimation(loop).Length);
        _customEnd = HasClip(end) ? end : "";
        return true;
    }

    /// <summary>
    /// A custom animation the model has no clips of: CUSTOM3-11 fall back to the engine's own animation of that
    /// number (<see cref="OpenWinter.Game.Animations.CustomFallback"/>); anything else plays as named.
    /// </summary>
    string CustomFallback(string clip) =>
        IsCustom(clip) && !HasClip(clip) && !HasClip("c" + clip) && OpenWinter.Game.Animations.CustomFallback(clip) is { } engine ? engine : clip;

    static bool IsCustom(string clip) =>
        clip.Length > 6 && clip.StartsWith("custom", StringComparison.OrdinalIgnoreCase) && int.TryParse(clip.AsSpan(6), out _);

    /// <summary>The custom animation playing ends with its closing clip; false when there is none to play.</summary>
    bool EndCustomSequence()
    {
        if (_customEnd.Length == 0 || _actionEnd <= 0 || !HasClip(_customEnd)) { _customEnd = ""; return false; }
        string end = _customEnd;
        _customEnd = "";
        ResetCastSequence();
        var clip = _library.GetAnimation(end);
        clip.LoopMode = Godot.Animation.LoopModeEnum.None;
        _actionClip.Animation = end;
        _actionClip.PlayMode = AnimationNodeAnimation.PlayModeEnum.Forward;
        ActionVersion++;
        _actionStarted = _clock;
        // Straight on from the loop's pose, not up from idle again.
        _actionShot.FadeInTime = 0;
        Set("parameters/action_seek/seek_request", 0.0);
        Set("parameters/action/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
        _actionEnd = _clock + Math.Max(0.1f, clip.Length);
        return true;
    }
}
