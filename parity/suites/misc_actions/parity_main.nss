// Odds and ends of the action queue: what a clock jump does to ActionWait and heartbeats,
// ActionUnlockObject, and ActionJumpToLocation / ActionSpeakString.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float f = 0.0) { return Location(Area(), Vector(x, y, 0.0), f); }

object Actor(string k, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "p_actor", L(x, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Mark(string s)
{
    PLog("jump.order", s);
}

void ClockJump()
{
    // A creature waiting 30 s and a delayed command, then the clock jumps an hour (120 s).
    object w = GetObjectByTag("WAITER");
    SetLocalFloat(GetModule(), "T_JUMP", PSince());
    SetLocalInt(GetModule(), "WATCH_HB", TRUE);
    SetTime(GetTimeHour() + 1, GetTimeMinute(), GetTimeSecond(), GetTimeMillisecond());
    DelayCommand(0.5, PLogI("jump.wait.action", GetCurrentAction(w)));
    DelayCommand(7.0, DeleteLocalInt(GetModule(), "WATCH_HB"));
}

void Unlock()
{
    object d = GetObjectByTag("D_PICK");
    object o = Actor("PICKER", 30.0, 6.0);
    SetLocalFloat(GetModule(), "T_DOOR", PSince());
    AssignCommand(o, ActionUnlockObject(d));
    AssignCommand(o, ActionDoCommand(PLogF("door.picker.done", PSince() - GetLocalFloat(GetModule(), "T_DOOR"), 0.12)));
    AssignCommand(o, ActionDoCommand(PLogI("door.locked.after", GetLocked(d))));
    DelayCommand(1.0, PLogI("door.picker.action", GetCurrentAction(o)));
}

void Others()
{
    object j = Actor("JUMPER", 10.0, 20.0);
    SetLocalFloat(GetModule(), "T_OTHER", PSince());
    AssignCommand(j, ActionJumpToLocation(L(14.0, 20.0, 45.0)));
    AssignCommand(j, ActionDoCommand(PLogF("jump.action.time", PSince() - GetLocalFloat(GetModule(), "T_OTHER"), 0.06)));
    AssignCommand(j, ActionSpeakString("hello"));
    AssignCommand(j, ActionDoCommand(PLogF("speak.action.time", PSince() - GetLocalFloat(GetModule(), "T_OTHER"), 0.06)));
    DelayCommand(0.3, PLogI("jump.waiting.action", GetCurrentAction(j)));
    DelayCommand(0.5, PLogF("jump.x", GetPosition(j).x, 0.01));
    DelayCommand(0.5, PLogF("jump.facing", GetFacing(j), 0.5));
}

void main()
{
    PStart();
    DelayCommand(1.0, Unlock());
    DelayCommand(1.0, Others());
    object w = Actor("WAITER", 20.0, 30.0);
    AssignCommand(w, ActionWait(30.0));
    AssignCommand(w, ActionDoCommand(PLog("jump.order", "wait.over")));
    CreateObject(OBJECT_TYPE_PLACEABLE, "p_plc", L(24.0, 30.0), FALSE, "HB_PLC");
    DelayCommand(9.0, ClockJump());
    DelayCommand(25.0, PDone());
}
