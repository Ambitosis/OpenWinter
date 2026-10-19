#include "visual_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    VHidePC(pc);
    object target = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    DelayCommand(1.0, VCamera(pc, 270.0, 12.0, 65.0));
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY,
        EffectVisualEffect(VFX_DUR_SPELLTURNING), target, 6.0));
    DelayCommand(2.5, VShot("half"));
    DelayCommand(4.5, VShot("double"));
    DelayCommand(6.5, VShot("half_again"));
    DelayCommand(9.0, VShot("removed"));
    DelayCommand(10.0, VDone());
}
