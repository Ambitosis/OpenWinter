using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// How a creature's conditions show on its body (server ObjectFlags; measured on the original with the
/// parity/visual scenes conditions and coupdegrace): asleep it lies face down, knocked down it sits on the ground,
/// held it stops moving where it stands, petrified it also turns to stone (progfx Petrify's vdu_tex_stone skin).
/// Stunned creatures' stars are drawn by EffectPlayer.
/// </summary>
public partial class CreatureView
{
    /// <summary>EffectLook keys for looks that come from a condition rather than from a visual effect.</summary>
    const long HeldLook = long.MinValue + 101, PetrifiedLook = long.MinValue + 102;

    ObjectFlags _shownConditions;
    bool _conditionsShown;

    void ApplyConditions(bool immediate)
    {
        const ObjectFlags mask = ObjectFlags.Asleep | ObjectFlags.KnockedDown | ObjectFlags.KnockedForward | ObjectFlags.Held | ObjectFlags.Petrified;
        var now = (Flags & (ObjectFlags.Dead | ObjectFlags.Dying)) != 0 ? ObjectFlags.None : Flags & mask;
        if (_conditionsShown && now == _shownConditions) return;
        // The way it fell is settled when it falls (the knockdown's creator may walk round it meanwhile).
        const ObjectFlags knocked = ObjectFlags.KnockedDown;
        if (_conditionsShown && (now & knocked) != 0 && (_shownConditions & knocked) != 0)
            now = (now & ~ObjectFlags.KnockedForward) | (_shownConditions & ObjectFlags.KnockedForward);
        if (_conditionsShown && now == _shownConditions) return;
        bool first = !_conditionsShown;
        _conditionsShown = true;
        _shownConditions = now;
        Animator?.SetDown((now & ObjectFlags.Asleep) != 0 ? Anim.CreatureAnimator.DownKind.Asleep
            : (now & ObjectFlags.KnockedDown) != 0 ? (now & ObjectFlags.KnockedForward) != 0 ? Anim.CreatureAnimator.DownKind.KnockedForward : Anim.CreatureAnimator.DownKind.KnockedDown
            : Anim.CreatureAnimator.DownKind.None, immediate || first);
        SetEffectLook(HeldLook, (now & ObjectFlags.Held) != 0 ? new EffectLook { FreezeAnimations = true } : null);
        Texture2D? stone = null;
        bool flip = false;
        if ((now & ObjectFlags.Petrified) != 0 && _presentationContent?.Textures.Get(PetrifySkin()) is { } skin) { stone = skin.Texture; flip = skin.FlipV; }
        SetEffectLook(PetrifiedLook, (now & ObjectFlags.Petrified) != 0 ? new EffectLook { FreezeAnimations = true, Skin = stone, SkinFlipV = flip } : null);
    }

    /// <summary>progfx.2da Petrify (row 1050): Param1 is the stone texture.</summary>
    string PetrifySkin()
    {
        var progfx = _presentationContent?.Resources.TwoDA("progfx");
        string tex = progfx?.Get(1050, "Param1") ?? "";
        return tex.Length > 0 && tex != "****" ? tex : "vdu_tex_stone";
    }

    /// <summary>A condition keeps the creature where it is: no walking or turning animation while it lies or sits.</summary>
    bool DownForCondition => Animator?.IsDown == true;
}
