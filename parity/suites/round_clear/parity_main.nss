// Combat rounds cleared mid-round.
// Attacker A (nw_bandit001, one attack a round, +20 to hit, no event scripts but a logging OnEndCombatRound)
// fights T1 1.5 m east; T2 stands 1.5 m west. Both targets are passive and immortal and log the swings
// (OnPhysicalAttacked) and blows (OnDamaged) they take. At A's second swing at T1 plus a delay:
//   re.<d>      ClearAllActions() then ActionAttack(T2), d = 0.5, 2, 4, 5.5 s (when does the first swing at T2 come?)
//   re.2.haste  as re.2 with A hasted
//   cut         ClearAllActions(TRUE) 0.2 s after the swing (does the blow land; does the round still end?)
//   cut.stock   as cut, A's OnEndCombatRound also runs the stock nw_c2_default3 (does it take the fight up again?)
// Times are seconds after the test acts ("x"), averaged over the group's attackers.
#include "rc_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
int nCell = 0;

object Make(float x, float y, float f, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(Area(), Vector(x, y, 0.0), f));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, nFaction);
    SetImmortal(o, TRUE);
    return o;
}

void Pair(string g, int nMode, float fDelay, int bHaste = FALSE, int bStock = FALSE)
{
    float x = 10.0 + IntToFloat(nCell % 6) * 15.0, y = 10.0 + IntToFloat(nCell / 6) * 15.0;
    nCell++;
    object a = Make(x, y, 0.0, STANDARD_FACTION_HOSTILE);
    object t1 = Make(x + 1.5, y, 180.0, STANDARD_FACTION_COMMONER);
    object t2 = Make(x - 1.5, y, 0.0, STANDARD_FACTION_COMMONER);
    SetLocalString(t1, "K", "s1");
    SetLocalString(t2, "K", "s2");
    SetEventScript(t1, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "rc_atk");
    SetEventScript(t2, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "rc_atk");
    SetEventScript(t1, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "rc_dmg");
    SetEventScript(t2, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "rc_dmg");
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, bStock ? "rc_eors" : "rc_eor");
    SetBaseAttackBonus(1, a);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), a);
    if (bHaste) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectHaste(), a);
    SetLocalObject(a, "T2", t2);
    SetLocalInt(a, "MODE", nMode);
    SetLocalFloat(a, "DELAY", fDelay);
    SetLocalString(a, "G", g);
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count");
    SetLocalObject(m, g + IntToString(n), a);
    SetLocalInt(m, g + ".count", n + 1);
    DelayCommand(2.0, RcMark(a, "o"));
    DelayCommand(2.0, AssignCommand(a, ActionAttack(t1)));
}

// The first time of a kind at or after the test acted, relative to it (-1 for none).
float After(object a, string sKind)
{
    float x = GetLocalFloat(a, "x0");
    int i, n = GetLocalInt(a, sKind + "n");
    for (i = 0; i < n; i++)
    {
        float t = GetLocalFloat(a, sKind + IntToString(i));
        if (t >= x - 0.001) return t - x;
    }
    return -1.0;
}

int CountAfter(object a, string sKind, float fFrom)
{
    float x = GetLocalFloat(a, "x0");
    int i, n = GetLocalInt(a, sKind + "n"), c = 0;
    for (i = 0; i < n; i++) if (GetLocalFloat(a, sKind + IntToString(i)) >= x + fFrom) c++;
    return c;
}

void Mean(string k, string g, string sKind, float fTol)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count"), i, c = 0;
    float sum = 0.0;
    for (i = 0; i < n; i++)
    {
        float v = After(GetLocalObject(m, g + IntToString(i)), sKind);
        if (v >= 0.0) { sum += v; c++; }
    }
    PLogF(k + ".n", IntToFloat(c), 1.0);
    if (c > 0) PLogF(k, sum / IntToFloat(c), fTol);
}

void Report(string g)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count"), i;
    Mean(g + ".s2", g, "s2", 0.4);
    Mean(g + ".e", g, "e", 0.4);
    if (GetStringLeft(g, 3) == "cut")
    {
        Mean(g + ".d1", g, "d1", 0.25);
        int later = 0;
        for (i = 0; i < n; i++)
        {
            object a = GetLocalObject(m, g + IntToString(i));
            later += CountAfter(a, "s1", 0.5) + CountAfter(a, "s2", 0.5);
        }
        PLogF(g + ".laterSwings", IntToFloat(later) / IntToFloat(n), 1.0);
    }
    // A's first swings before the test: order to first swing, the gap between the first two, and the second swing's
    // time after the round end before it.
    float first = 0.0, gap = 0.0, eo = 0.0;
    int ce = 0;
    for (i = 0; i < n; i++)
    {
        object a = GetLocalObject(m, g + IntToString(i));
        first += GetLocalFloat(a, "s10") - GetLocalFloat(a, "o0");
        gap += GetLocalFloat(a, "s11") - GetLocalFloat(a, "s10");
        if (GetLocalInt(a, "en") > 0 && GetLocalFloat(a, "e0") < GetLocalFloat(a, "s11"))
        {
            eo += GetLocalFloat(a, "s11") - GetLocalFloat(a, "e0");
            ce++;
        }
        WriteTimestampedLogEntry("RAW|" + g + IntToString(i) + "|" + GetLocalString(a, "SEQ"));
    }
    PLogF(g + ".first", first / IntToFloat(n), 0.4);
    PLogF(g + ".period", gap / IntToFloat(n), 0.3);
    if (ce > 0) PLogF(g + ".swingAfterEor", eo / IntToFloat(ce), 0.3);
}

void main()
{
    PStart();
    int i;
    for (i = 0; i < 4; i++)
    {
        Pair("re.0.5", 1, 0.5);
        Pair("re.2", 1, 2.0);
        Pair("re.4", 1, 4.0);
        Pair("re.5.5", 1, 5.5);
        Pair("re.2.haste", 1, 2.0, TRUE);
        Pair("cut", 2, 0.2);
    }
    for (i = 0; i < 3; i++) Pair("cut.stock", 2, 0.2, FALSE, TRUE);
    string gs = "re.0.5 re.2 re.4 re.5.5 re.2.haste cut cut.stock ";
    while (gs != "")
    {
        int sp = FindSubString(gs, " ");
        DelayCommand(40.0, Report(GetStringLeft(gs, sp)));
        gs = GetSubString(gs, sp + 1, 1000);
    }
    DelayCommand(41.0, PDone());
}
