// Emitter calibration: this scene's own text model (vdr_spellturn.mdl, made by tools/visual/emitcal_model.py,
// shipped in a scene hak) takes the place of the spell turning glow on a bandit and shows emitters with known
// settings beside him: a column (velocity and life), a square (size), a fall (mass), additive and blended squares
// (blending), tinted and untinted squares (m_isTinted), a colour ramp and a cone (spread).
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    object o = VSpawn(oPC, "nw_bandit001", 0.0, 6.0, 270.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(VFX_DUR_SPELLTURNING), o);
    DelayCommand(3.0, VCamera(oPC, 90.0, 16.0, 80.0));
    DelayCommand(6.0, VShot("a_side"));
    DelayCommand(7.0, VCamera(oPC, 90.0, 14.0, 45.0));
    DelayCommand(9.0, VShot("b_above"));
    DelayCommand(11.0, VDone());
}
