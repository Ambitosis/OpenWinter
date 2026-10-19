// An archer close to its target: does it step back before shooting, and does stepping back draw attacks of
// opportunity? A drow archer (short bow) attacks a bandit 1.5 m away; the bandit is passive, or fights back in melee.
// Logs how far the archer is from where it started and from its target over time, and attacks per round on it.
#include "pcombat_inc"

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Archer(string g, float fDist, int bFightBack)
{
    object a = PPair(g, "nw_drowfight010", "nw_bandit001", fDist);
    SetBaseAttackBonus(1, a);
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    PUnequip(a, INVENTORY_SLOT_LEFTHAND);
    PEquip(a, "nw_wbwsh001", INVENTORY_SLOT_RIGHTHAND);
    PEquip(a, "nw_wamar001", INVENTORY_SLOT_ARROWS, 99);
    object t = GetLocalObject(a, "T");
    SetLocalLocation(a, "L0", GetLocation(a));
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_arch_atk");
    if (bFightBack)
    {
        SetBaseAttackBonus(1, t);
        DelayCommand(1.0, AssignCommand(t, ActionAttack(a)));
    }
    return a;
}

void Sample(string g, string sAt)
{
    object o = GetFirstObjectInArea(GetObjectByTag("PARITY_AREA"));
    float moved = 0.0, apart = 0.0;
    int n = 0;
    while (GetIsObjectValid(o))
    {
        if (GetLocalString(o, "G") == g && GetIsObjectValid(GetLocalObject(o, "T")))
        {
            moved += GetDistanceBetweenLocations(GetLocation(o), GetLocalLocation(o, "L0"));
            apart += GetDistanceBetween(o, GetLocalObject(o, "T"));
            n++;
        }
        o = GetNextObjectInArea(GetObjectByTag("PARITY_AREA"));
    }
    if (n == 0) return;
    // 2.5 s after the shot about half the archers have taken their first 0.8 m step (archer_backoff), so a mean over
    // four varies by about 0.3 m between runs.
    float tol = sAt == "3s" ? 1.0 : 0.6;
    PLogF(g + ".moved." + sAt, moved / IntToFloat(n), tol);
    PLogF(g + ".apart." + sAt, apart / IntToFloat(n), tol);
}

void Setup()
{
    int i;
    for (i = 0; i < 4; i++)
    {
        Archer("close", 1.5, FALSE);
        Archer("close.fight", 1.5, TRUE);
        Archer("near", 3.0, FALSE);
        Archer("far", 6.0, FALSE);
    }
    PStartAll(1.0);
    string s = "close close.fight near far ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        DelayCommand(2.0, Sample(g, "1s"));
        DelayCommand(4.0, Sample(g, "3s"));
        // Later on, an archer that started 1.5 m away fares differently from run to run on nwserver (its passive target
        // sometimes takes to fighting back), so only the first step is compared for those.
        if (GetStringLeft(g, 5) != "close")
        {
            DelayCommand(8.0, Sample(g, "7s"));
            DelayCommand(19.0, Sample(g, "18s"));
        }
        s = GetSubString(s, sp + 1, 1000);
    }
}

void Report()
{
    string s = "near far ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PReportRounds(g);
        PLogMean(g + ".attacks.on.archer", IntToFloat(PGet(g + ".onarcher")), 4, 1.0, 0.5);
        s = GetSubString(s, sp + 1, 1000);
    }
    PDone();
}

void main()
{
    PClockStart(40.0);
    DelayCommand(1.0, Setup());
    DelayCommand(26.0, Report());
}
