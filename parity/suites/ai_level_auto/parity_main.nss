// Automatic AI levels without players.
// Each creature's GetAILevel is sampled every 0.25 s and logged as a "level@time" sequence of its changes.
//   auto     made at run time, never set: the level with no players (expected VERY_LOW)
//   fight    attacks a plot commoner from 2 s to 10 s, then ClearAllActions (expected: NORMAL in combat, back to VERY_LOW
//            about 8 s after combat was last refreshed); its victim is "victim"
//   high / vhigh / neg / big   SetAILevel HIGH, VERY_HIGH (expected: clamped to HIGH), -5 (expected: VERY_LOW), 9 (expected: HIGH)
//   lowlock  SetAILevel LOW, then the same fight (expected: locked, stays LOW)
//   neglock  SetAILevel -5, then the same fight (expected: locked at VERY_LOW)
//   unlock   SetAILevel NORMAL; AI_LEVEL_DEFAULT at 6 s (expected: back to the rule level at once)
//   unlockf  SetAILevel NORMAL, the fight, AI_LEVEL_DEFAULT at 6 s while fighting (expected: NORMAL)
//   hench    an NPC's henchman (expected: HIGH only for a player's associate)
//   jumplow  SetAILevel LOW, jumps to the second area at 4 s (expected: arrivals change unlocked levels only)
//   plc      a placeable
#include "parity_inc"
#include "parity_time"

object Mk(string sName, float x, float y)
{
    object c = CreateObject(OBJECT_TYPE_CREATURE, "al_c", Location(GetObjectByTag("AL_A"), Vector(x, y, 0.0), 0.0), FALSE, "ALX_" + sName);
    SetLocalString(GetModule(), "LIST", GetLocalString(GetModule(), "LIST") + sName + " ");
    ChangeToStandardFaction(c, STANDARD_FACTION_COMMONER);
    return c;
}

object Victim(object a)
{
    object v = CreateObject(OBJECT_TYPE_CREATURE, "al_c", Location(GetArea(a), GetPosition(a) + Vector(1.6, 0.0, 0.0), 180.0), FALSE, GetTag(a) + "_V");
    ChangeToStandardFaction(v, STANDARD_FACTION_COMMONER);
    SetPlotFlag(v, TRUE);
    ChangeToStandardFaction(a, STANDARD_FACTION_HOSTILE);
    SetLocalObject(a, "V", v);
    return v;
}

void Attack(object a) { AssignCommand(a, ActionAttack(GetLocalObject(a, "V"))); }
void Stop(object a) { AssignCommand(a, ClearAllActions(TRUE)); }

void Sample()
{
    string l = GetLocalString(GetModule(), "LIST");
    float t = PSince();
    while (l != "")
    {
        int p = FindSubString(l, " ");
        string n = GetStringLeft(l, p);
        l = GetSubString(l, p + 1, 10000);
        object o = GetObjectByTag("ALX_" + n);
        int v = GetAILevel(o);
        object m = GetModule();
        if (!GetLocalInt(m, "HAS_" + n) || GetLocalInt(m, "LV_" + n) != v)
        {
            SetLocalInt(m, "HAS_" + n, TRUE);
            SetLocalInt(m, "LV_" + n, v);
            SetLocalString(m, "SEQ_" + n, GetLocalString(m, "SEQ_" + n) + IntToString(v) + "@" + FloatToString(t, 0, 2) + " ");
        }
        int f = GetIsInCombat(o);
        if (!GetLocalInt(m, "HASC_" + n) || GetLocalInt(m, "CB_" + n) != f)
        {
            SetLocalInt(m, "HASC_" + n, TRUE);
            SetLocalInt(m, "CB_" + n, f);
            SetLocalString(m, "CSEQ_" + n, GetLocalString(m, "CSEQ_" + n) + IntToString(f) + "@" + FloatToString(t, 0, 2) + " ");
        }
    }
    if (t < 30.0) DelayCommand(0.25, Sample());
}

void Report()
{
    string l = GetLocalString(GetModule(), "LIST");
    while (l != "")
    {
        int p = FindSubString(l, " ");
        string n = GetStringLeft(l, p);
        l = GetSubString(l, p + 1, 10000);
        PLogSeq(n + ".level", GetLocalString(GetModule(), "SEQ_" + n), 0.8);
        WriteTimestampedLogEntry("RAW|" + n + ".combat|" + GetLocalString(GetModule(), "CSEQ_" + n));
    }
    PDone();
}

void main()
{
    PStart();
    Mk("auto", 10.0, 10.0);
    object f = Mk("fight", 10.0, 20.0); Victim(f);
    SetLocalString(GetModule(), "LIST", GetLocalString(GetModule(), "LIST") + "fight_V ");
    SetAILevel(Mk("high", 10.0, 30.0), AI_LEVEL_HIGH);
    SetAILevel(Mk("vhigh", 10.0, 40.0), AI_LEVEL_VERY_HIGH);
    SetAILevel(Mk("neg", 10.0, 50.0), -5);
    SetAILevel(Mk("big", 10.0, 60.0), 9);
    object ll = Mk("lowlock", 30.0, 10.0); Victim(ll); SetAILevel(ll, AI_LEVEL_LOW);
    object nl = Mk("neglock", 30.0, 20.0); Victim(nl); SetAILevel(nl, -5);
    object u = Mk("unlock", 30.0, 30.0); SetAILevel(u, AI_LEVEL_NORMAL);
    object uf = Mk("unlockf", 30.0, 40.0); Victim(uf); SetAILevel(uf, AI_LEVEL_NORMAL);
    object hm = Mk("henchm", 50.0, 10.0);
    object h = Mk("hench", 52.0, 10.0);
    AddHenchman(hm, h);
    object j = Mk("jumplow", 50.0, 30.0); SetAILevel(j, AI_LEVEL_LOW);
    CreateObject(OBJECT_TYPE_PLACEABLE, "al_p", Location(GetObjectByTag("AL_A"), Vector(60.0, 60.0, 0.0), 0.0), FALSE, "ALX_plc");
    SetLocalString(GetModule(), "LIST", GetLocalString(GetModule(), "LIST") + "plc ");
    PLogI("now.high", GetAILevel(GetObjectByTag("ALX_high")));
    PLogI("now.neg", GetAILevel(GetObjectByTag("ALX_neg")));
    PLogI("now.big", GetAILevel(GetObjectByTag("ALX_big")));
    PLogI("now.auto", GetAILevel(GetObjectByTag("ALX_auto")));
    Sample();
    DelayCommand(2.0, Attack(f)); DelayCommand(2.0, Attack(ll)); DelayCommand(2.0, Attack(nl)); DelayCommand(2.0, Attack(uf));
    DelayCommand(4.0, AssignCommand(j, JumpToLocation(Location(GetObjectByTag("AL_B"), Vector(20.0, 20.0, 0.0), 0.0))));
    DelayCommand(6.0, SetAILevel(u, AI_LEVEL_DEFAULT));
    DelayCommand(6.0, SetAILevel(uf, AI_LEVEL_DEFAULT));
    DelayCommand(10.0, Stop(f)); DelayCommand(10.0, Stop(ll)); DelayCommand(10.0, Stop(nl)); DelayCommand(10.0, Stop(uf));
    DelayCommand(31.0, Report());
}
