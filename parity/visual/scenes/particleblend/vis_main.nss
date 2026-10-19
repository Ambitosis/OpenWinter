#include "visual_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    VHidePC(pc);
    object target = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    DelayCommand(1.0, VCamera(pc, 90.0, 16.0, 75.0));
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY,
        EffectVisualEffect(VFX_DUR_SPELLTURNING), target, 4.0));
    DelayCommand(4.0, VShot("blend_grid"));
    DelayCommand(7.0, VShot("removed"));
    DelayCommand(8.0, VDone());
}
