// How badly a disarm in combat must miss to set the trap off (expected: by 11 or more). Rogues with Disable Trap
// 10 + 2 (Set Trap synergy) attacked by hostile creatures that can't hurt them try hostile trap triggers (which the
// hostiles' faction would set off on them): group A against DC 27 (rolls 1-14 miss by 1 to 14), group B against
// DC 46 (every roll misses, by 14 to 33).
#include "ptrap_inc"

object Actor(string sTag, string sRes, float x, float y, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, PTrapL(x, y), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetPlotFlag(o, TRUE);
    return o;
}

void Case(string k, float x, float y, int nOver)
{
    object t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(x, y), 1.0, "C_" + k, STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    object a = Actor("A_" + k, "tn_rogue", x - 1.6, y, STANDARD_FACTION_COMMONER);
    object h = Actor("H_" + k, "nw_bandit001", x - 2.6, y, STANDARD_FACTION_HOSTILE);
    AssignCommand(h, ActionAttack(a));
    SetTrapDisarmDC(t, GetSkillRank(SKILL_DISABLE_TRAP, a) + nOver);
    SetTrapDetectedBy(t, a, TRUE);
}

void Setup()
{
    int i;
    for (i = 0; i < 30; i++) Case("A" + IntToString(i), 5.0 + 7.0 * IntToFloat(i % 10), 5.0 + 6.0 * IntToFloat(i / 10), 17);
    for (i = 0; i < 30; i++) Case("B" + IntToString(i), 5.0 + 7.0 * IntToFloat(i % 10), 30.0 + 6.0 * IntToFloat(i / 10), 36);
}

void Go()
{
    int i;
    for (i = 0; i < 30; i++)
    {
        string k = "A" + IntToString(i);
        object a = GetObjectByTag("A_" + k);
        SetLocalInt(a, "COMBAT", GetIsInCombat(a));
        AssignCommand(a, ActionUseSkill(SKILL_DISABLE_TRAP, GetObjectByTag("C_" + k), 0));
        k = "B" + IntToString(i);
        a = GetObjectByTag("A_" + k);
        SetLocalInt(a, "COMBAT", GetIsInCombat(a));
        AssignCommand(a, ActionUseSkill(SKILL_DISABLE_TRAP, GetObjectByTag("C_" + k), 0));
    }
}

void Count(string g)
{
    int i, nOk = 0, nSprung = 0, nCombat = 0;
    for (i = 0; i < 30; i++)
    {
        string k = g + IntToString(i);
        string ev = GetLocalString(GetModule(), "PTO_C_" + k);
        nOk += FindSubString(ev, "disarm") >= 0;
        nSprung += FindSubString(ev, "trig") >= 0;
        nCombat += GetLocalInt(GetObjectByTag("A_" + k), "COMBAT");
    }
    PLogI(g + ".incombat", nCombat);
    PLog(g + ".disarmed", IntToString(nOk) + "|~6");
    PLog(g + ".sprung", IntToString(nSprung) + "|~6");
}

void main()
{
    DelayCommand(1.0, Setup());
    DelayCommand(4.0, Go());
    DelayCommand(16.0, Count("A"));
    DelayCommand(16.0, Count("B"));
    DelayCommand(17.0, PDone());
}
