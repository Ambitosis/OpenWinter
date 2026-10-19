// A creature leaving the area or destroyed (expected:
// every observer drops it at once, with OnPerception "inaudible" then "vanished"; the leaver's own list is emptied
// without events). Pairs of plain bandits (no stealth, commoner and merchant factions) 3 m apart in SN_A0, both
// logging OnPerception. At 12 s (each has seen and heard the other by then) the target T of each pair:
//   jump     jumps to SN_A1;   destroy  is destroyed;   walk  walks 25 m away (out of the 20 m ranges), as a control
// Logged per pair: O's events and T's events after the act (times from the act), and O's GetObjectSeen/Heard of T
// 0.1 s and 0.5 s after it.
#include "parity_inc"
#include "parity_time"

void Pair(string g, int m)
{
    object a = GetObjectByTag("SN_A0");
    float x = 6.0 + IntToFloat(m) * 22.0, y = g == "jump" ? 6.0 : g == "destroy" ? 30.0 : 54.0;
    object o = CreateObject(OBJECT_TYPE_CREATURE, "pl_c", Location(a, Vector(x, y, 0.0), 0.0));
    object t = CreateObject(OBJECT_TYPE_CREATURE, "pl_c", Location(a, Vector(x + 3.0, y, 0.0), 180.0));
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(t, STANDARD_FACTION_MERCHANT);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetAILevel(t, AI_LEVEL_NORMAL);
    SetLocalObject(o, "T", t);
    SetLocalObject(t, "O", o);
    SetLocalObject(GetModule(), g + IntToString(m), o);
}

void Read(object o, object t, string k)
{
    PLogI(k + ".seen", GetObjectSeen(t, o));
    PLogI(k + ".heard", GetObjectHeard(t, o));
}

void Act(string g, int m)
{
    object o = GetLocalObject(GetModule(), g + IntToString(m));
    object t = GetLocalObject(o, "T");
    SetLocalFloat(o, "X", PSince());
    SetLocalFloat(t, "X", PSince());
    if (g == "jump") AssignCommand(t, JumpToLocation(Location(GetObjectByTag("SN_A1"), Vector(10.0 + IntToFloat(m) * 10.0, 10.0, 0.0), 0.0)));
    else if (g == "destroy") DestroyObject(t);
    else AssignCommand(t, ActionMoveToLocation(Location(GetArea(o), GetPosition(t) + Vector(0.0, 25.0, 0.0), 0.0), TRUE));
    string k = g + IntToString(m);
    DelayCommand(0.1, Read(o, t, k + ".d01"));
    DelayCommand(0.5, Read(o, t, k + ".d05"));
}

void Report(string g, int m)
{
    object o = GetLocalObject(GetModule(), g + IntToString(m));
    object t = GetLocalObject(o, "T");
    WriteTimestampedLogEntry("RAW|" + g + IntToString(m) + ".O|" + GetLocalString(o, "EV"));
    WriteTimestampedLogEntry("RAW|" + g + IntToString(m) + ".T|" + GetLocalString(t, "EV"));
    // The observer's first event after the act: its kind sequence (the first two), and its time.
    string ev = GetLocalString(o, "EV");
    PLog(g + IntToString(m) + ".first2", GetStringLeft(ev, 2) + (GetStringLength(ev) > 9 ? "," + GetSubString(ev, FindSubString(ev, " ") + 1, 2) : ""));
    if (ev != "") PLogF(g + IntToString(m) + ".at", StringToFloat(GetSubString(ev, 3, FindSubString(ev, " ") - 3)), 0.6);
}

void main()
{
    PStart();
    int m;
    string gs = "jump destroy walk ";
    while (gs != "")
    {
        int sp = FindSubString(gs, " ");
        string g = GetStringLeft(gs, sp);
        gs = GetSubString(gs, sp + 1, 1000);
        for (m = 0; m < 3; m++)
        {
            Pair(g, m);
            DelayCommand(12.0 + IntToFloat(m) * 0.37, Act(g, m));
            DelayCommand(30.0, Report(g, m));
        }
    }
    DelayCommand(31.0, PDone());
}
