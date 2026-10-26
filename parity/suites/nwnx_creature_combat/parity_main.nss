// NWNX_Creature settings in a fight: the bonus attack limit on EffectModifyAttacks, critical range and multiplier
// overrides, attack roll overrides, and who is flat-footed while it goes on.
#include "pcombat_inc"
#include "nwnx_creature"

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

// A squad of bandits on a passive bandit, each with one scripted attack per round.
object Squad(string g, int n)
{
    object t = PSquad(g, "nw_bandit001", "nw_bandit001", n);
    PSquadAttacks(t, 1);
    return t;
}

void ForSquad(object t, int nWhat, int nValue)
{
    object o = GetFirstObjectInArea(GetArea(t));
    while (GetIsObjectValid(o))
    {
        if (GetLocalObject(o, "T") == t)
        {
            if (nWhat == 1) NWNX_Creature_SetMaximumBonusAttacks(o, nValue);
            if (nWhat == 2) { NWNX_Creature_SetCriticalRangeOverride(o, nValue); NWNX_Creature_SetCriticalMultiplierOverride(o, 6); }
            if (nWhat == 3) { int r; for (r = 11; r <= 20; r++) NWNX_Creature_SetAttackRollOverride(o, r, nValue); }
        }
        o = GetNextObjectInArea(GetArea(t));
    }
}

void Flat(object t)
{
    object a = GetFirstObjectInArea(GetArea(t));
    while (GetIsObjectValid(a) && GetLocalObject(a, "T") != t) a = GetNextObjectInArea(GetArea(t));
    PLog("flat", IntToString(NWNX_Creature_GetFlatFooted(t)) + " " + IntToString(NWNX_Creature_GetFlatFooted(a)) + " "
        + IntToString(NWNX_Creature_GetIsFlanking(a, t)));
}

void Report()
{
    PReportRounds("base");
    PReportRounds("mba2");
    PReportRounds("mba7");
    PReportRounds("neg");
    PReportHits("crit");
    int hits = PGet("crit.hit");
    PLogRate("crit.critrate", PGet("crit.crit"), hits);
    PReportHits("plain");
    PLogRate("plain.critrate", PGet("plain.crit"), PGet("plain.hit"));
    PReportHits("aro");
    PReportHits("armour");
    PDone();
}

void main()
{
    PClockStart(46.0);
    // Five bonus attacks; at most two; ten held to seven.
    int i;
    for (i = 0; i < 2; i++)
    {
        object a = PPair("base", "nw_bandit001", "nw_bandit001");
        SetBaseAttackBonus(1, a);
        Perm(a, EffectModifyAttacks(5));
        a = PPair("mba2", "nw_bandit001", "nw_bandit001");
        SetBaseAttackBonus(1, a);
        Perm(a, EffectModifyAttacks(5));
        NWNX_Creature_SetMaximumBonusAttacks(a, 2);
        a = PPair("mba7", "nw_bandit001", "nw_bandit001");
        SetBaseAttackBonus(1, a);
        Perm(a, EffectModifyAttacks(5));
        Perm(a, EffectModifyAttacks(5));
        NWNX_Creature_SetMaximumBonusAttacks(a, 7);
        // A negative EffectModifyAttacks beside haste.
        a = PPair("neg", "nw_bandit001", "nw_bandit001");
        SetBaseAttackBonus(1, a);
        Perm(a, EffectModifyAttacks(-1));
        Perm(a, EffectHaste());
    }
    object t;

    // Criticals from a 2-20 threat range at x6: a critical hit does 18 or more, an ordinary one at most 10.
    t = Squad("crit", 4);
    PSquadEffect(t, EffectModifyAttacks(3));
    SetLocalInt(GetModule(), "crit.critat", 18);
    ForSquad(t, 2, 2);
    t = Squad("plain", 4);
    PSquadEffect(t, EffectModifyAttacks(3));
    SetLocalInt(GetModule(), "plain.critat", 18);

    // Against armour class 50: rolls of 11 and up with +100 hit, the rest miss (a 20 included).
    t = Squad("aro", 4);
    PSquadEffect(t, EffectModifyAttacks(3));
    Perm(t, EffectACIncrease(20, AC_DEFLECTION_BONUS));
    Perm(t, EffectACIncrease(20, AC_NATURAL_BONUS));
    ForSquad(t, 3, 100);
    t = Squad("armour", 4);
    PSquadEffect(t, EffectModifyAttacks(3));
    Perm(t, EffectACIncrease(20, AC_DEFLECTION_BONUS));
    Perm(t, EffectACIncrease(20, AC_NATURAL_BONUS));

    PStartAll(1.0);
    DelayCommand(8.0, Flat(t));
    DelayCommand(44.0, Report());
}
