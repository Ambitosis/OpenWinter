// In the Prelude's first room Bim stands between the player and the door M1Q0A_M1Q0B, and
// the player walks round him. Scripted moves (the same MoveAction as a click in OpenWinter) from a point west of
// Bim: to the door (click range 1.8 m), ActionOpenDoor, a point just past Bim and a point inside his personal
// space. Notes log the player's path every 0.2 s. No cutscene mode (see main). Bim's and Pavel's heartbeat and notice scripts are cleared.
#include "visual_inc"
object Bim() { return GetObjectByTag("M1Q0ABim"); }
object Door() { return GetObjectByTag("M1Q0A_M1Q0B"); }
location L(float x, float y, float facing) { return Location(GetArea(GetFirstPC()), Vector(x, y, 0.0), facing); }
void Reset()
{
    object p = GetFirstPC();
    AssignCommand(Bim(), ClearAllActions(TRUE));
    AssignCommand(Bim(), JumpToLocation(L(24.93374, 16.063742, 0.0)));
    VMovePC(p, 21.0, 16.5, 0.0, 0.0);
}
void Note(string name)
{
    object p = GetFirstPC();
    vector v = GetPosition(p), b = GetPosition(Bim());
    WriteTimestampedLogEntry("VISUAL|note|" + name + "|pc=" + FloatToString(v.x, 0, 3) + "," + FloatToString(v.y, 0, 3)
        + "|action=" + IntToString(GetCurrentAction(p)) + "|bim=" + FloatToString(b.x, 0, 3) + "," + FloatToString(b.y, 0, 3)
        + "|dist=" + FloatToString(GetDistanceBetween(p, Bim()), 0, 3) + "|friend=" + IntToString(GetIsFriend(Bim(), p))
        + "|seen=" + IntToString(GetObjectSeen(Bim(), p)) + "|open=" + IntToString(GetIsOpen(Door())));
}
void Door18() { AssignCommand(GetFirstPC(), ActionMoveToObject(Door(), TRUE, 1.8)); }
void Open() { AssignCommand(GetFirstPC(), ActionOpenDoor(Door())); }
void Past() { AssignCommand(GetFirstPC(), ActionMoveToLocation(L(26.6, 15.6, 0.0), TRUE)); }
void Near() { AssignCommand(GetFirstPC(), ActionMoveToLocation(L(25.9, 16.2, 0.0), TRUE)); }
void Trial(float t, string name, int kind)
{
    DelayCommand(t, Reset());
    if (kind == 0) DelayCommand(t + 1.5, Door18());
    else if (kind == 1) DelayCommand(t + 1.5, Open());
    else if (kind == 2) DelayCommand(t + 1.5, Past());
    else DelayCommand(t + 1.5, Near());
    int i; for (i = 0; i < 25; i++) DelayCommand(t + 1.5 + IntToFloat(i) * 0.2, Note(name + IntToString(i)));
    DelayCommand(t + 6.6, VShot(name));
}
void main()
{
    object p = GetEnteringObject(); if (!GetIsPC(p)) return;
    // Not VBegin: cutscene mode lets the player walk through creatures (a first run walked straight through Bim).
    SetCameraMode(p, CAMERA_MODE_TOP_DOWN); SetCameraLimits(p, 1.0, 89.0, 1.0, 40.0); SetPlotFlag(p, TRUE);
    object o;
    for (o = GetFirstObjectInArea(GetArea(p)); GetIsObjectValid(o); o = GetNextObjectInArea(GetArea(p)))
        if (GetObjectType(o) == OBJECT_TYPE_CREATURE && !GetIsPC(o))
        {
            SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
            SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_NOTICE, "");
            SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_DIALOGUE, "");
        }
    DelayCommand(2.0, VCamera(p, 90.0, 9.0, 20.0));
    Trial(4.0, "door", 0); Trial(12.0, "open", 1); Trial(20.0, "past", 2); Trial(28.0, "near", 3);
    DelayCommand(37.0, VDone());
}
