#include "visual_inc"
// loadtips: what an area load puts in the chat window (the original copies "Loading Screen:" hints there), on the
// module's first load and on transitions to two other areas and back; and the faction iteration scripts see for a
// player with henchmen (GetFactionLeader, GetFirst/NextFactionMember with bPCOnly TRUE and FALSE, from the player
// and from a henchman). Each step is announced in the chat window ("LT <step>").

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|lt|" + s); }
void Mark(string sStep) { SendMessageToPC(GetFirstPC(), "LT " + sStep); Log("step|" + sStep); }

string Who(object o) { return GetIsObjectValid(o) ? GetName(o) + "(" + GetTag(o) + ")" : "INVALID"; }

void Faction(string sLabel, object oFrom)
{
    string s = sLabel + "|leader=" + Who(GetFactionLeader(oFrom)) + "|pcOnly=";
    object m = GetFirstFactionMember(oFrom, TRUE);
    int n = 0;
    while (GetIsObjectValid(m) && n < 20) { s += Who(m) + ","; n++; m = GetNextFactionMember(oFrom, TRUE); }
    s += "|all=";
    m = GetFirstFactionMember(oFrom, FALSE);
    n = 0;
    while (GetIsObjectValid(m) && n < 20) { s += Who(m) + ","; n++; m = GetNextFactionMember(oFrom, FALSE); }
    s += "|avgLevel=" + IntToString(GetFactionAverageLevel(oFrom)) + "|avgXP=" + IntToString(GetFactionAverageXP(oFrom))
        + "|strongest=" + Who(GetFactionStrongestMember(oFrom, FALSE)) + "|weakest=" + Who(GetFactionWeakestMember(oFrom, FALSE))
        + "|isPC=" + IntToString(GetIsPC(oFrom)) + "|sameFaction=" + IntToString(GetFactionEqual(oFrom, GetFirstPC()));
    Log(s);
}

// sArea: an area tag, or "" for the scene's own area (NWScript defers a delayed call's arguments too).
void Jump(string sArea)
{
    object pc = GetFirstPC();
    object oArea = sArea == "" ? GetLocalObject(GetModule(), "lt_main") : GetObjectByTag(sArea);
    Mark("jump_" + GetTag(oArea));
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(oArea, Vector(10.0, 10.0, 0.0), 90.0)));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    object mod = GetModule();
    int n = GetLocalInt(mod, "lt_enter") + 1;
    SetLocalInt(mod, "lt_enter", n);
    Mark("enter_main_" + IntToString(n));
    if (n > 1)
    {
        DelayCommand(6.0, Mark("end"));
        DelayCommand(7.0, VDone());
        return;
    }
    SetLocalObject(mod, "lt_main", GetArea(pc));
    SetPlotFlag(pc, TRUE);
    SetMaxHenchmen(4);
    DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 55.0));
    vector v = GetPosition(pc);
    object h1 = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(v.x + 2.0, v.y, 0.0), 0.0), FALSE, "lt_h1");
    object h2 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(GetArea(pc), Vector(v.x - 2.0, v.y, 0.0), 0.0), FALSE, "lt_h2");
    ChangeToStandardFaction(h1, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(h2, STANDARD_FACTION_COMMONER);
    SetPlotFlag(h1, TRUE);
    SetPlotFlag(h2, TRUE);
    DelayCommand(2.0, Faction("alone", pc));
    DelayCommand(2.5, AddHenchman(pc, h1));
    DelayCommand(3.0, Faction("hench1", pc));
    DelayCommand(3.5, AddHenchman(pc, h2));
    DelayCommand(4.0, Faction("hench2", pc));
    DelayCommand(4.2, Faction("fromHench1", h1));
    DelayCommand(4.4, Faction("fromHench2", h2));
    DelayCommand(5.0, Mark("before_jumps"));
    DelayCommand(10.0, Jump("LT_FAR"));
    DelayCommand(23.0, Jump("LT_FAR2"));
    DelayCommand(37.0, Jump("LT_FAR"));
    DelayCommand(49.0, Jump("LT_FAR2"));
    DelayCommand(61.0, Jump("LT_FAR"));
    DelayCommand(73.0, Jump(""));
}
