#include "visual_inc"

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    VHidePC(pc);
    object bat = VSpawn(pc, "nw_fm_bat01", 0.0, 1.0, 270.0);
    SetEventScript(bat, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    DelayCommand(1.0, VCamera(pc, 90.0, 7.0, 65.0));
    DelayCommand(3.0, VShot("a_baseline"));
    DelayCommand(4.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectAppear(), bat));
    DelayCommand(4.2, VShot("b_appear_early"));
    DelayCommand(4.9, VShot("b_appear_mid"));
    DelayCommand(6.3, VShot("b_appear_end"));
    DelayCommand(8.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDisappear(), bat, 3.0));
    DelayCommand(8.2, VShot("c_disappear_early"));
    DelayCommand(8.9, VShot("c_disappear_mid"));
    DelayCommand(10.7, VShot("c_disappear_hidden"));
    DelayCommand(11.5, VShot("d_return"));
    DelayCommand(14.0, VDone());
}
