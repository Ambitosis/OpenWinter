// Event timing: OnModuleLoad / OnSpawn / OnEnter order, when a created creature's OnSpawn runs, the
// shared order of DelayCommand / AssignCommand / SignalEvent, and heartbeat periods and phases for
// module, area, placeables and creatures (whose beats are jittered: compared as statistics).
#include "parity_inc"
#include "parity_time"

void Mark(string s)
{
    PLog("order", s + " self=" + GetTag(OBJECT_SELF));
}

void Stats(string sGroup)
{
    object m = GetModule();
    string k = "HB_" + sGroup;
    int n = GetLocalInt(m, k + "_N"), f = GetLocalInt(m, k + "_FN");
    PLogI("hbstat." + sGroup + ".beats", n > 0);
    PLogI("hbstat." + sGroup + ".firsts", f);
    if (n == 0 || f == 0) return;
    PLogF("hbstat." + sGroup + ".mean", GetLocalFloat(m, k + "_SUM") / IntToFloat(n), 0.3);
    PLogF("hbstat." + sGroup + ".min", GetLocalFloat(m, k + "_MIN"), 0.4);
    PLogF("hbstat." + sGroup + ".max", GetLocalFloat(m, k + "_MAX"), 0.4);
    PLogF("hbstat." + sGroup + ".first.mean", GetLocalFloat(m, k + "_FSUM") / IntToFloat(f), 0.5);
}

void Stop()
{
    SetLocalInt(GetModule(), "PARITY_STOP", 1);
    Stats("NORMAL");
    Stats("VERYLOW");
    PDone();
}

// Creatures created in a script: OnEnter and OnSpawn run after the script, OnSpawn as soon as
// something is sent to the creature, else after the queued events.
void Create()
{
    PLog("order", "create.begin");
    object a = CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(-4.0, 4.0), FALSE, "A");
    SetLocalInt(a, "SET_AFTER", 1);
    PLog("order", "created spawned=" + IntToString(GetLocalInt(a, "SPAWNED")) + " ai=" + IntToString(GetAILevel(a)));
    object b = CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(-2.0, 4.0), FALSE, "B");
    object c = CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(0.0, 4.0), FALSE, "C");
    object d = CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(2.0, 4.0), FALSE, "D");
    object e = CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(4.0, 4.0), FALSE, "E");
    object p = CreateObject(OBJECT_TYPE_PLACEABLE, "p_hbplc", PCentre(4.0, -4.0), FALSE, "MADE_PLC");
    DelayCommand(0.0, Mark("delay0.first"));
    AssignCommand(c, Mark("assign.c"));
    AssignCommand(b, DelayCommand(0.0, Mark("assign.delay.b")));
    AssignCommand(d, ActionDoCommand(Mark("action.d")));
    SignalEvent(e, EventUserDefined(5));
    SignalEvent(GetModule(), EventUserDefined(6));
    SignalEvent(p, EventUserDefined(7));
    AssignCommand(GetModule(), Mark("assign.module"));
    DelayCommand(0.0, Mark("delay0.last"));
    PLog("order", "create.end");
}

void Herd()
{
    SetLocalFloat(GetModule(), "HB_CREATED", PSince());
    int i;
    for (i = 0; i < 10; i++)
    {
        object n = CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(IntToFloat(i * 2) - 9.0, 8.0), FALSE, "HBN_" + IntToString(i));
        SetAILevel(n, AI_LEVEL_NORMAL);
        CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(IntToFloat(i * 2) - 9.0, -8.0), FALSE, "HBV_" + IntToString(i));
    }
}

void main()
{
    PStart();
    object c = GetObjectByTag("GIT_CRIT");
    PLog("order", "modload crit=" + IntToString(GetIsObjectValid(c)) + " spawned=" + IntToString(GetLocalInt(c, "SPAWNED"))
        + " ai=" + IntToString(GetAILevel(c)));
    DelayCommand(0.0, Mark("modload.delay0"));
    DelayCommand(1.0, Create());
    DelayCommand(1.5, Herd());
    DelayCommand(31.0, Stop());
}
