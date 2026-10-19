using Godot;

namespace OpenWinter.Client.Anim;

public partial class CreatureAnimator
{
    bool _effectFrozen;
    AnimationCallbackModeProcess _unfrozenProcess;

    /// <summary>ProgFX FreezeAnimations keeps the current bone pose until the last effect ends.</summary>
    public void SetEffectFrozen(bool frozen)
    {
        if (_effectFrozen == frozen) return;
        _effectFrozen = frozen;
        if (frozen)
        {
            _unfrozenProcess = CallbackModeProcess;
            // Manual processing leaves the current skeleton pose and graph position intact.
            // Disabling the tree instead would deactivate its blend state.
            CallbackModeProcess = AnimationCallbackModeProcess.Manual;
        }
        else CallbackModeProcess = _unfrozenProcess;
    }
}
