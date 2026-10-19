// Click feedback. The harness posts clicks without touching the real cursor. Check shots.txt for actual
// movement on the original: an ignored background click is NOT evidence that the original has no marker.
// The optional tag lets OpenWinter aim at the object despite its different camera framing.
#include "visual_inc"

void Click(string sPoint, string sTag = "")
{
    WriteTimestampedLogEntry("VISUAL|click|" + sPoint + "|" + sTag);
}

void Reset(object oPC)
{
    VMovePC(oPC, 20.0, 15.0, 0.0, 90.0);
    VCamera(oPC, 90.0, 12.0, 45.0);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(oPC, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(oPC, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectMovementSpeedDecrease(60), oPC);
    object n = VSpawn(oPC, "nw_bandit001", -3.0, 4.0, 270.0);
    SetTag(n, "reticle_npc");
    AssignCommand(n, SetCommandable(FALSE, n));
    object p = VPlaceable(oPC, "plc_chest1", 3.0, 4.0);
    SetTag(p, "reticle_chest");
    object h = VSpawn(oPC, "nw_goblina", 0.0, 8.0, 270.0);
    SetTag(h, "reticle_enemy");
    ChangeToStandardFaction(h, STANDARD_FACTION_HOSTILE);
    AdjustReputation(oPC, h, -100);
    AssignCommand(h, SetCommandable(FALSE, h));
    DelayCommand(3.0, Reset(oPC));
    DelayCommand(5.0, VShot("a_before"));
    DelayCommand(6.0, Click("0.68,0.55"));
    DelayCommand(6.2, VShot("b_move_start"));
    DelayCommand(6.7, VShot("c_move_pulse"));
    DelayCommand(13.0, VShot("d_move_arrived"));
    DelayCommand(14.0, Reset(oPC));
    DelayCommand(16.0, Click("0.36,0.30", "reticle_npc"));
    DelayCommand(16.5, VShot("e_talk_approach"));
    DelayCommand(20.0, Reset(oPC));
    DelayCommand(22.0, Click("0.64,0.34", "reticle_chest"));
    DelayCommand(22.5, VShot("f_chest_approach"));
    DelayCommand(29.0, VShot("g_chest_arrived"));
    DelayCommand(30.0, Reset(oPC));
    DelayCommand(32.0, Click("0.50,0.16", "reticle_enemy"));
    DelayCommand(32.5, VShot("h_enemy_approach"));
    DelayCommand(38.0, VShot("i_enemy_fighting"));
    DelayCommand(39.0, Reset(oPC));
    DelayCommand(41.0, VShot("j_cancelled"));
    DelayCommand(42.0, VDone());
}
