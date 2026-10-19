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
        EffectVisualEffect(VFX_DUR_BIGBYS_INTERPOSING_HAND), target, 4.0));
    DelayCommand(2.1, VShot("growing"));
    DelayCommand(2.5, VShot("grown"));
    DelayCommand(3.5, VShot("duration"));
    DelayCommand(5.8, VShot("before_end"));
    DelayCommand(6.1, VShot("shrinking"));
    DelayCommand(6.5, VShot("removed"));
    DelayCommand(8.0, VDone());
}
