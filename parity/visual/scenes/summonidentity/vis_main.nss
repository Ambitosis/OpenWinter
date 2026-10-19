#include "visual_inc"

void Fx(object target)
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(VFX_IMP_POLYMORPH), target);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    object bat = VSpawn(pc, "nw_fm_bat01", 3.0, 0.0, 90.0);
    SetCommandable(FALSE, bat);
    DelayCommand(1.0, VCamera(pc, 90.0, 7.0, 65.0));
    DelayCommand(4.0, AssignCommand(pc, SummonFamiliar()));
    DelayCommand(4.15, VShot("a_familiar_early"));
    DelayCommand(4.7, VShot("a_familiar"));
    DelayCommand(5.5, VShot("a_familiar_late"));
    DelayCommand(8.0, Fx(pc));
    DelayCommand(8.7, VShot("b_polymorph_medium"));
    DelayCommand(12.0, Fx(bat));
    DelayCommand(12.7, VShot("c_polymorph_tiny"));
    DelayCommand(16.0, VDone());
}
