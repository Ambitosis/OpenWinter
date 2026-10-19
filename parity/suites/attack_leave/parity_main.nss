// An attack whose target leaves the area. No players.
// Each group has its own pair of areas joined by an open door (G_A -> G_B); AL_Z is joined to nothing. A Hostile
// attacker fights a plot commoner 2 m away from 1 s; at 5 s the target jumps away:
//   EX   ActionAttack(t, FALSE), target jumps to G_B           expected: follows it there and fights on
//   PS   ActionAttack(t, TRUE), target jumps to G_B            expected: the attack ends
//   PSE  as PS with a second commoner 1.5 m from the attacker  expected: switches to it
//   EXE  as EX with that second commoner                       expected: follows (the switch is only for passive attacks
//                                                              and attackers that make no progress)
//   NR   ActionAttack(t, FALSE), target jumps to AL_Z          expected: no route; the NPC keeps trying
//   NRE  as NR with the second commoner                        expected: switches to it
// Sampled every 0.25 s from 5 s: the attacker's area, current action and attack target, as change sequences.
#include "parity_inc"
#include "parity_time"

object Mk(string sArea, float x, float y, string sTag, int nFaction)
{
    object c = CreateObject(OBJECT_TYPE_CREATURE, "al_c", Location(GetObjectByTag(sArea), Vector(x, y, 0.0), 90.0), FALSE, sTag);
    ChangeToStandardFaction(c, nFaction);
    SetAILevel(c, AI_LEVEL_NORMAL);
    if (nFaction != STANDARD_FACTION_HOSTILE) SetPlotFlag(c, TRUE);
    return c;
}

void Group(string g)
{
    Mk(g + "_A", 15.0, 11.0, g + "_ATK", STANDARD_FACTION_HOSTILE);
    Mk(g + "_A", 15.0, 13.0, g + "_T", STANDARD_FACTION_COMMONER);
    if (GetStringRight(g, 1) == "E") Mk(g + "_A", 13.5, 11.0, g + "_E", STANDARD_FACTION_COMMONER);
    object d = GetObjectByTag(g + "_AB"); AssignCommand(d, ActionOpenDoor(d));
    object r = GetObjectByTag(g + "_AB_R"); AssignCommand(r, ActionOpenDoor(r));
    SetLocalString(GetModule(), "GROUPS", GetLocalString(GetModule(), "GROUPS") + g + " ");
}

void Attack(string g)
{
    int bPassive = GetStringLeft(g, 1) == "P";
    AssignCommand(GetObjectByTag(g + "_ATK"), ActionAttack(GetObjectByTag(g + "_T"), bPassive));
}

void Leave(string g, int i)
{
    object t = GetObjectByTag(g + "_T");
    location l = GetStringLeft(g, 2) == "NR" ? Location(GetObjectByTag("AL_Z"), Vector(10.0 + IntToFloat(i) * 8.0, 20.0, 0.0), 0.0)
                                             : Location(GetObjectByTag(g + "_B"), Vector(15.0, 15.0, 0.0), 0.0);
    AssignCommand(t, ClearAllActions());
    AssignCommand(t, JumpToLocation(l));
}

string Who(object o, string g)
{
    if (!GetIsObjectValid(o)) return "none";
    string t = GetTag(o);
    return GetStringLeft(t, GetStringLength(g) + 1) == g + "_" ? GetSubString(t, GetStringLength(g) + 1, 10) : t;
}

void Track(string k, string v, float t)
{
    object m = GetModule();
    if (GetLocalString(m, "V_" + k) == v && GetLocalInt(m, "H_" + k)) return;
    SetLocalInt(m, "H_" + k, TRUE);
    SetLocalString(m, "V_" + k, v);
    SetLocalString(m, "S_" + k, GetLocalString(m, "S_" + k) + v + "@" + FloatToString(t, 0, 2) + " ");
}

void Sample()
{
    string l = GetLocalString(GetModule(), "GROUPS");
    float t = PSince();
    while (l != "")
    {
        int p = FindSubString(l, " ");
        string g = GetStringLeft(l, p);
        l = GetSubString(l, p + 1, 10000);
        object a = GetObjectByTag(g + "_ATK");
        Track(g + ".area", GetTag(GetArea(a)), t);
        Track(g + ".action", IntToString(GetCurrentAction(a)), t);
        Track(g + ".target", Who(GetAttackTarget(a), g), t);
        vector v = GetPosition(a);
        Track(g + ".pos", GetTag(GetArea(a)) + ":" + IntToString(FloatToInt(v.x)) + "," + IntToString(FloatToInt(v.y)), t);
    }
    if (t < 24.0) DelayCommand(0.25, Sample());
}

void Report()
{
    string l = GetLocalString(GetModule(), "GROUPS");
    while (l != "")
    {
        int p = FindSubString(l, " ");
        string g = GetStringLeft(l, p);
        l = GetSubString(l, p + 1, 10000);
        PLogSeq(g + ".area", GetLocalString(GetModule(), "S_" + g + ".area"), 1.0);
        WriteTimestampedLogEntry("RAW|" + g + ".action|" + GetLocalString(GetModule(), "S_" + g + ".action"));
        WriteTimestampedLogEntry("RAW|" + g + ".target|" + GetLocalString(GetModule(), "S_" + g + ".target"));
        WriteTimestampedLogEntry("RAW|" + g + ".pos|" + GetLocalString(GetModule(), "S_" + g + ".pos"));
        object a = GetObjectByTag(g + "_ATK");
        PLogI(g + ".end.action", GetCurrentAction(a));
        if (GetStringLeft(g, 1) == "P") PLog(g + ".end.target", Who(GetAttackTarget(a), g));
        PLog(g + ".end.area", GetTag(GetArea(a)));
    }
    PDone();
}

void main()
{
    PStart();
    string s = "EX PS PSE EXE NR NRE ";
    int i = 0;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string g = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 1000);
        Group(g);
        DelayCommand(1.0, Attack(g));
        DelayCommand(5.0, Leave(g, i));
        i++;
    }
    DelayCommand(5.0, Sample());
    DelayCommand(25.0, Report());
}
