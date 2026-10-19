#include "visual_inc"

void Impact(object target, int vfx)
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(vfx), target);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    DelayCommand(1.0, VCamera(pc, 270.0, 10.0, 65.0));
    DelayCommand(3.0, VShot("baseline"));
    DelayCommand(4.0, Impact(pc, 71));
    DelayCommand(4.10, VShot("aid_010"));
    DelayCommand(4.60, VShot("aid_060"));
    DelayCommand(5.10, VShot("aid_110"));
    DelayCommand(5.40, VShot("aid_140"));
    DelayCommand(5.80, VShot("aid_180"));
    DelayCommand(6.40, VShot("aid_240"));
    DelayCommand(8.0, VShot("aid_cleanup"));
    DelayCommand(9.0, Impact(pc, 36));
    DelayCommand(9.10, VShot("summon_010"));
    DelayCommand(9.60, VShot("summon_060"));
    DelayCommand(10.10, VShot("summon_110"));
    DelayCommand(10.40, VShot("summon_140"));
    DelayCommand(10.80, VShot("summon_180"));
    DelayCommand(11.40, VShot("summon_240"));
    DelayCommand(13.0, VShot("summon_cleanup"));
    DelayCommand(14.0, VDone());
}
