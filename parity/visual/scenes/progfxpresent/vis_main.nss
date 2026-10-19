#include "visual_inc"

void Fx(object target, int vfx, float duration = 0.0)
{
    ApplyEffectToObject(duration > 0.0 ? DURATION_TYPE_TEMPORARY : DURATION_TYPE_INSTANT,
        EffectVisualEffect(vfx), target, duration);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    VHidePC(pc);
    object target = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    SetEventScript(target, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    DelayCommand(1.0, VCamera(pc, 90.0, 7.0, 65.0));
    DelayCommand(3.0, VShot("a_baseline"));
    DelayCommand(4.0, Fx(target, 292));
    DelayCommand(4.1, VShot("b_spellfail_head"));
    DelayCommand(7.0, Fx(target, 293));
    DelayCommand(7.1, VShot("c_spellfail_hand"));
    DelayCommand(10.0, Fx(target, 548, 2.0));
    DelayCommand(10.6, VShot("d_aura_red"));
    DelayCommand(13.0, Fx(target, 553, 2.0));
    DelayCommand(13.6, VShot("e_aura_white"));
    DelayCommand(16.0, Fx(target, 513, 3.0));
    DelayCommand(16.1, VShot("f_pulse_0"));
    DelayCommand(16.35, VShot("f_pulse_1"));
    DelayCommand(16.6, VShot("f_pulse_2"));
    DelayCommand(16.85, VShot("f_pulse_3"));
    DelayCommand(20.0, VShot("g_expired"));
    DelayCommand(21.0, AssignCommand(target, PlayAnimation(ANIMATION_LOOPING_CONJURE1, 1.0, 6.0)));
    DelayCommand(21.3, Fx(target, 352, 2.0));
    DelayCommand(21.5, VShot("h_freeze_start"));
    DelayCommand(22.4, VShot("h_freeze_hold"));
    DelayCommand(24.0, VShot("h_freeze_end"));
    DelayCommand(25.0, Fx(target, 294));
    DelayCommand(25.1, VShot("i_flash_early"));
    DelayCommand(25.4, VShot("i_flash_mid"));
    DelayCommand(25.8, VShot("i_flash_late"));
    DelayCommand(28.0, VDone());
}
