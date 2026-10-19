// Search: which creatures find traps by themselves, from how far (passive and in detect mode, chests and
// trap triggers), how soon, half the skill when passive (full with Keen Sense), rogue-only DCs, and traps
// that can't be detected or are inactive.
#include "ptrap_inc"
#include "parity_time"

object Actor(string sTag, string sRes, location l)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, l, FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

// The case list, for polling.
void Add(string k)
{
    object m = GetModule();
    int n = GetLocalInt(m, "NCASE");
    SetLocalString(m, "CASE" + IntToString(n), k);
    SetLocalInt(m, "NCASE", n + 1);
}

// A trap (chest, or a 2 m trap trigger) at (x, y) and an actor fDist metres north of its centre.
void Case(string k, string sRes, float x, float y, float fDist, int nDC, int nBonus, int nTrigger = FALSE, int nDetectMode = FALSE)
{
    object t;
    if (nTrigger) t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(x, y), 2.0, "T_" + k, STANDARD_FACTION_HOSTILE);
    else
    {
        t = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(x, y), FALSE, "T_" + k);
        CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, t, STANDARD_FACTION_HOSTILE);
    }
    SetTrapDetectDC(t, nDC);
    object a = Actor("A_" + k, sRes, PTrapL(x, y + fDist, 270.0));
    if (nBonus > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SEARCH, nBonus), a);
    if (nDetectMode) SetActionMode(a, ACTION_MODE_DETECT, TRUE);
    SetLocalObject(a, "TRAP", t);
    Add(k);
}

void Poll(int nLeft)
{
    object m = GetModule();
    int i, n = GetLocalInt(m, "NCASE");
    for (i = 0; i < n; i++)
    {
        string k = GetLocalString(m, "CASE" + IntToString(i));
        object a = GetObjectByTag("A_" + k);
        if (GetLocalInt(a, "SEEN")) continue;
        if (GetTrapDetectedBy(GetLocalObject(a, "TRAP"), a))
        {
            SetLocalInt(a, "SEEN", 1);
            SetLocalFloat(a, "AT", PSince());
        }
    }
    if (nLeft > 0) DelayCommand(0.25, Poll(nLeft - 1));
}

void Report()
{
    object m = GetModule();
    int i, n = GetLocalInt(m, "NCASE");
    int nHP = 0, nHA = 0, nK = 0;
    for (i = 0; i < n; i++)
    {
        string k = GetLocalString(m, "CASE" + IntToString(i));
        object a = GetObjectByTag("A_" + k);
        int nSeen = GetLocalInt(a, "SEEN");
        if (GetStringLeft(k, 3) == "HP_") nHP += nSeen;
        else if (GetStringLeft(k, 3) == "HA_") nHA += nSeen;
        else if (GetStringLeft(k, 2) == "K_") nK += nSeen;
        else
        {
            PLogI(k + ".seen", nSeen);
            // Searching happens once a round, each creature on its own clock.
            if (nSeen) PLogF(k + ".at", GetLocalFloat(a, "AT"), 6.5);
            PLogI(k + ".mode", GetActionMode(a, ACTION_MODE_DETECT));
        }
    }
    // Rates out of 8 (about 30% a round over four rounds when the full skill counts, never at half).
    PLogI("passive.half.seen", nHP);
    PLog("active.full.seen", IntToString(nHA) + "|~4");
    PLog("keen.full.seen", IntToString(nK) + "|~4");
    object a = GetObjectByTag("A_RC2");
    PLog("last.RC2", PTrapTag(GetLastTrapDetected(a)));
}

void main()
{
    PStart();
    int i;
    // Passive range to a chest: +50 Search against DC 1.
    float d;
    i = 0;
    for (d = 2.0; d <= 20.0; d += 2.0) { Case("RC" + FloatToString(d, 0, 0), "tq_rogue", 5.0 + 9.0 * i, 5.0, d, 1, 50); i++; }
    // Passive range to a 2 m trap trigger (its centre fDist away; the edge one metre closer).
    i = 0;
    for (d = 3.0; d <= 13.0; d += 2.0) { Case("RT" + FloatToString(d, 0, 0), "tq_rogue", 5.0 + 9.0 * i, 30.0, d, 1, 50, TRUE); i++; }
    // In detect mode.
    i = 0;
    for (d = 6.0; d <= 24.0; d += 2.0) { Case("AC" + FloatToString(d, 0, 0), "tq_rogue", 5.0 + 9.0 * i, 50.0, d, 1, 50, FALSE, TRUE); i++; }
    // Half the skill when passive: rank 10 against DC 26.
    for (i = 0; i < 8; i++) Case("HP_" + IntToString(i), "tq_rogue", 5.0 + 9.0 * i, 80.0, 2.0, 26, 0);
    for (i = 0; i < 8; i++) Case("HA_" + IntToString(i), "tq_rogue", 5.0 + 9.0 * i, 88.0, 2.0, 26, 0, FALSE, TRUE);
    for (i = 0; i < 8; i++) Case("K_" + IntToString(i), "tq_keen", 5.0 + 9.0 * i, 96.0, 2.0, 26, 0);
    // Rogue-only DCs.
    Case("F35", "tq_fighter", 5.0, 104.0, 2.0, 35, 50);
    Case("F36", "tq_fighter", 14.0, 104.0, 2.0, 36, 50);
    Case("R36", "tq_rogue", 23.0, 104.0, 2.0, 36, 50);
    Case("FA36", "tq_fighter", 32.0, 104.0, 2.0, 36, 50, FALSE, TRUE);
    // Not detectable / inactive.
    Case("UNDET", "tq_rogue", 41.0, 104.0, 2.0, 1, 50);
    SetTrapDetectable(GetObjectByTag("T_UNDET"), FALSE);
    Case("INACTIVE", "tq_rogue", 50.0, 104.0, 2.0, 1, 50);
    SetTrapActive(GetObjectByTag("T_INACTIVE"), FALSE);
    // A creature with no Search ranks at all, +50 from an effect.
    Case("NORANK", "nw_bandit001", 59.0, 104.0, 2.0, 1, 50);
    DelayCommand(0.5, Poll(110));
    DelayCommand(29.0, Report());
    DelayCommand(30.0, PDone());
}
