#include "visual_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    VHidePC(pc);
    object target = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    DelayCommand(1.0, VCamera(pc, 90.0, 8.0, 65.0));
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY,
        EffectVisualEffect(VFX_DUR_SPELLTURNING), target, 13.0));
    DelayCommand(4.5, VShot("low_025"));
    DelayCommand(5.0, VShot("low_030"));
    DelayCommand(7.5, VShot("high_055"));
    DelayCommand(8.0, VShot("high_060"));
    DelayCommand(9.5, VShot("draining"));
    DelayCommand(11.0, VShot("stopped"));
    DelayCommand(14.0, VShot("low_resume"));
    DelayCommand(16.0, VShot("removed"));
    DelayCommand(17.0, VDone());
}
