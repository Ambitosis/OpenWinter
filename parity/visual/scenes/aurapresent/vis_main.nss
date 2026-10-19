#include "visual_inc"

void Aura(object actor, int vfx, float duration)
{
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectVisualEffect(vfx), actor, duration);
}

void Tab(string state)
{
    WriteTimestampedLogEntry("VISUAL|key|9|" + state);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    // Preserve pointer/Tab interaction so the fixture also tests ownership of the glow.
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    VHidePC(pc);
    object actor = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    SetEventScript(actor, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    DelayCommand(1.0, VCamera(pc, 90.0, 7.0, 65.0));
    DelayCommand(3.0, VShot("baseline"));
    DelayCommand(4.0, Aura(actor, 548, 3.0));
    DelayCommand(4.6, VShot("red"));
    DelayCommand(5.0, Tab("down"));
    DelayCommand(5.3, VShot("red_tab"));
    DelayCommand(5.6, Tab("up"));
    DelayCommand(5.9, VShot("red_restored"));
    DelayCommand(7.3, VShot("red_expired"));
    DelayCommand(8.0, Aura(actor, 553, 2.0));
    DelayCommand(8.6, VShot("white"));
    DelayCommand(10.3, VShot("white_expired"));
    DelayCommand(11.0, Aura(actor, 548, 4.0));
    DelayCommand(11.1, Aura(actor, 553, 1.0));
    DelayCommand(11.6, VShot("overlap_white"));
    DelayCommand(12.4, VShot("overlap_red_restored"));
    DelayCommand(12.7, Aura(actor, 11, 2.0));
    DelayCommand(13.2, VShot("skin_glow"));
    DelayCommand(13.6, Aura(actor, 355, 0.6));
    DelayCommand(13.9, VShot("hidden_glow"));
    DelayCommand(14.4, VShot("visible_again"));
    DelayCommand(15.5, VShot("all_expired"));
    DelayCommand(16.0, VDone());
}
