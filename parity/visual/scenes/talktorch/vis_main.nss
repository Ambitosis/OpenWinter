// A creature holding a torch in conversation: the original lets the raised torch down while talking and the talk
// animations swing it with the arm; it is raised again when the conversation ends.
#include "visual_inc"
void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }
void BeginAsSelf(string sDlg, object oPC) { BeginConversation(sDlg, oPC); }
void Shots(string s, float t)
{
    DelayCommand(t + 0.3, VShot(s + "_0")); DelayCommand(t + 0.8, VShot(s + "_1")); DelayCommand(t + 1.4, VShot(s + "_2"));
    DelayCommand(t + 2.2, VShot(s + "_3")); DelayCommand(t + 3.2, VShot(s + "_4"));
}
void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(oPC, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(oPC, TRUE);
    object oA = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(oPC), Vector(20.0, 15.0, 0.0), 270.0));
    ChangeToStandardFaction(oA, STANDARD_FACTION_MERCHANT);
    SetPlotFlag(oA, TRUE);
    object oT = CreateItemOnObject("nw_it_torch001", oA);
    AssignCommand(oA, ActionEquipItem(oT, INVENTORY_SLOT_LEFTHAND));
    DelayCommand(1.0, VCamera(oPC, 80.0, 4.5, 74.0));
    DelayCommand(3.0, AssignCommand(oA, BeginAsSelf("vis_tt", oPC)));
    Shots("t0", 3.0);
    DelayCommand(7.0, Tap(49)); Shots("t38", 7.0);
    DelayCommand(11.0, Tap(49)); Shots("t40", 11.0);
    DelayCommand(15.0, Tap(49)); Shots("t41", 15.0);
    DelayCommand(19.0, Tap(49));
    DelayCommand(19.5, VShot("t_end"));
    DelayCommand(21.0, VDone());
}
