#include "visual_inc"

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    DelayCommand(1.0, VCamera(pc, 90.0, 11.0, 65.0));
    DelayCommand(4.0, AssignCommand(pc, SummonFamiliar()));
    DelayCommand(4.7, VShot("a_familiar_marker"));
    DelayCommand(8.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(VFX_FNF_SUMMON_MONSTER_1), pc));
    DelayCommand(8.7, VShot("b_control_marker"));
    DelayCommand(12.0, VDone());
}
