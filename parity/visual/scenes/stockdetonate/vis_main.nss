#include "visual_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    DelayCommand(1.0, VCamera(pc, 270.0, 14.0, 65.0));
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(183), pc));
    DelayCommand(2.10, VShot("sonic_010"));
    DelayCommand(2.50, VShot("sonic_050"));
    DelayCommand(3.0, VShot("sonic_100"));
    DelayCommand(3.60, VShot("sonic_160"));
    DelayCommand(4.20, VShot("sonic_220"));
    DelayCommand(6.0, VShot("sonic_end"));
    DelayCommand(7.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(39), pc));
    DelayCommand(7.5, VShot("wail_050"));
    DelayCommand(9.80, VShot("wail_before"));
    DelayCommand(10.15, VShot("wail_event"));
    DelayCommand(10.60, VShot("wail_later"));
    DelayCommand(14.0, VShot("wail_end"));
    DelayCommand(15.0, VDone());
}
