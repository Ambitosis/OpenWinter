#include "visual_inc"
#include "cv_inc"
// Follow-up to convobusy3: a player in combat starting a conversation with herself (the private "tool" dialogs
// persistent worlds open from an item) and with a placeable. A creature asking a player in combat got
// "You are too excited to talk right now." (convobusy2).
object Dummy() { return GetObjectByTag("probe_dummy"); }
object Plc() { return GetObjectByTag("probe_plc"); }
void Strip(object o) { int ev; for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, ""); SetPlotFlag(o, TRUE); }
void Case(string sName)
{
    object pc = GetFirstPC(); object a = GetArea(pc);
    SetLocalString(GetModule(), "case", sName);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(a, Vector(8.0, 8.0, 0.0), 0.0)));
    object d = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(a, Vector(8.0, 9.0, 0.0), 270.0), FALSE, "probe_dummy");
    ChangeToStandardFaction(d, STANDARD_FACTION_HOSTILE); Strip(d);
    CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chair", Location(a, Vector(10.0, 8.0, 0.0), 180.0), FALSE, "probe_plc");
    DelayCommand(0.2, Note("begin"));
    DelayCommand(0.5, AssignCommand(d, ActionAttack(pc)));
}
void Clean()
{
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions(TRUE));
    object d = Dummy(); SetPlotFlag(d, FALSE); DestroyObject(d);
    object p = Plc(); SetPlotFlag(p, FALSE); DestroyObject(p);
}
void Asks(object oTo) { object pc = GetFirstPC(); AssignCommand(pc, ActionStartConversation(oTo, "cv_talk", TRUE, FALSE)); Note("asked"); }
void Poll(int k) { Note("t" + IntToString(k)); if (k < 6) DelayCommand(0.75, Poll(k + 1)); }
void Esc() { if (IsInConversation(GetFirstPC())) WriteTimestampedLogEntry("VISUAL|key|27|tap"); }

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 55.0));
    float t = 4.0;
    // 0: attacked for 4 s, the player starts a private conversation with herself.
    DelayCommand(t, Case("pc_combat_self")); DelayCommand(t + 4.5, Asks(pc)); DelayCommand(t + 4.5, Poll(0));
    DelayCommand(t + 6.0, VShot("pc_combat_self")); DelayCommand(t + 9.5, Esc()); DelayCommand(t + 11.0, Clean());
    t += 12.0;
    // 1: ... with a chair beside her.
    DelayCommand(t, Case("pc_combat_placeable")); DelayCommand(t + 4.5, Asks(Plc())); DelayCommand(t + 4.5, Poll(0));
    DelayCommand(t + 9.5, Esc()); DelayCommand(t + 11.0, Clean());
    t += 12.0;
    DelayCommand(t, VDone());
}
