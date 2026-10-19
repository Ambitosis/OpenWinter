#include "visual_inc"

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    VHidePC(pc);
    object actor = VSpawn(pc, "nw_bandit001", 0.0, 2.0, 270.0);
    DelayCommand(1.0, VCamera(pc, 90.0, 9.0, 65.0));
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY,
        EffectVisualEffect(VFX_DUR_SPELLTURNING), actor, 5.0));
    int i;
    for (i = 0; i < 12; i++)
        DelayCommand(3.1 + IntToFloat(i) * 0.25, VShot("frame_" + IntToString(i)));
    DelayCommand(7.25, VShot("stopping_025"));
    DelayCommand(8.25, VShot("stopping_125"));
    DelayCommand(9.25, VShot("removed_225"));
    DelayCommand(10.0, VDone());
}
