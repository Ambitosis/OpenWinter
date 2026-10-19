#include "visual_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    VHidePC(pc);
    object target = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 65.0));
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY,
        EffectVisualEffect(VFX_DUR_SPELLTURNING), target, 7.0));
    DelayCommand(2.5, VShot("before_event"));
    DelayCommand(3.5, VShot("first_burst"));
    DelayCommand(4.5, VShot("overlap"));
    DelayCommand(6.5, VShot("second_only"));
    DelayCommand(7.5, VShot("drained"));
    DelayCommand(10.0, VShot("removed"));
    DelayCommand(11.0, VDone());
}
