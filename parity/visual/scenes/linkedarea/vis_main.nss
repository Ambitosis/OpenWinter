// Linked emitters: this scene's own text model (vdr_spellturn.mdl) takes the place of the spell turning glow on a
// bandit 6 m north of the hidden player. At the player a linked emitter born over a 4 x 2 m area (as ground mists use
// them), 4 m north the same with a 4x4 flipbook texture, 4 m east one born at a point rising straight up (a ring texture), 6 m east the same with a 2x2 sheet of symbols, and 6 m
// west one moving east 1 m above the ground. Views from above, from the south and from the east.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    object o = VSpawn(oPC, "nw_bandit001", 0.0, 6.0, 270.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(VFX_DUR_SPELLTURNING), o);
    DelayCommand(3.0, VCamera(oPC, 90.0, 14.0, 1.0));
    DelayCommand(8.0, VShot("a_above"));
    DelayCommand(9.0, VCamera(oPC, 90.0, 12.0, 85.0));
    DelayCommand(12.0, VShot("b_south"));
    DelayCommand(13.0, VCamera(oPC, 180.0, 12.0, 85.0));
    DelayCommand(16.0, VShot("c_east"));
    DelayCommand(17.0, VCamera(oPC, 90.0, 12.0, 60.0));
    DelayCommand(20.0, VShot("d_south_high"));
    DelayCommand(21.0, VDone());
}
