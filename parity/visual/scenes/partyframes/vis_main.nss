// The classic party bar with members under it: three henchmen (one at half health) and a summoned creature.
// Measures the member frames' size, their place under pnl_party_bar and their spacing.
// OpenWinter's side needs --ui classic to compare.
#include "visual_inc"

object Hench(object oPC, float fDx, string sName)
{
    object o = VSpawn(oPC, "nw_bandit001", fDx, 3.0, 270.0);
    SetName(o, sName);
    AddHenchman(oPC, o);
    return o;
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetPlotFlag(oPC, TRUE);
    SetMaxHenchmen(3);
    DelayCommand(1.0, VCamera(oPC, 90.0, 8.0, 60.0));
    object a = Hench(oPC, -1.5, "Hench A");
    object b = Hench(oPC, 1.5, "Hench B");
    Hench(oPC, 0.0, "Hench C");
    SetPlotFlag(b, FALSE);
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(GetMaxHitPoints(b) / 2), b));
    location l = Location(GetArea(oPC), GetPosition(oPC) + Vector(0.0, -2.0, 0.0), 90.0);
    // Cast from the player, as a summoning spell does, so the creature is the player's associate.
    DelayCommand(2.0, AssignCommand(oPC, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire"), l, 120.0)));
    DelayCommand(8.0, VShot("a_party"));
    DelayCommand(11.0, VDone());
}
