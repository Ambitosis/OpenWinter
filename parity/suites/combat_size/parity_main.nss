// Size and the other attack bonus and armour class terms. Touch attacks (TouchAttackMelee/Ranged,
// thousands per configuration) measure them precisely: the target's size counts for AC though GetAC
// leaves it out, but the attacker's size does not count for touch attacks. Weapon attacks by squads of
// resized attackers (SetCreatureAppearanceType) show the attacker's size does count for them.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";
// Appearances by size: imp (tiny), halfling (small), ogre (large), fire giant (huge).
const int TINY = 105;
const int SMALL = 3;
const int LARGE = 127;
const int HUGE = 80;

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

// ---------------------------------------------------------------- touch attacks

void Chunk(string k, object a, object t, int bRanged, int n)
{
    int i;
    for (i = 0; i < n; i++)
    {
        int r = bRanged ? TouchAttackRanged(t, FALSE, a) : TouchAttackMelee(t, FALSE, a);
        PAdd(k + ".r" + IntToString(r));
    }
}

void TouchReport(string k)
{
    PLogRate("touch." + k, PGet(k + ".r1") + PGet(k + ".r2"), PGet(k + ".r0") + PGet(k + ".r1") + PGet(k + ".r2"));
}

// 4000 touch attacks by a resized nw_drowfight010 with -12 attack on a resized nw_bandit001.
void Touch(string k, int nAttApp, int nTgtApp, int bRanged = FALSE, int nStr = 0, int nDex = 0, int nAB = 0, int nACType = -1)
{
    int n = PGet("PAIRS");
    PAdd("PAIRS");
    object a = CreateObject(OBJECT_TYPE_CREATURE, FIGHTER, PCell(n, 0.0));
    object t = CreateObject(OBJECT_TYPE_CREATURE, TARGET, PCell(n, 1.5));
    PStrip(a);
    PStrip(t);
    if (nAttApp >= 0) SetCreatureAppearanceType(a, nAttApp);
    if (nTgtApp >= 0) SetCreatureAppearanceType(t, nTgtApp);
    Perm(a, EffectAttackDecrease(12));
    if (nStr > 0) Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, nStr));
    if (nDex > 0) Perm(a, EffectAbilityIncrease(ABILITY_DEXTERITY, nDex));
    if (nAB > 0) Perm(a, EffectAttackIncrease(nAB));
    if (nACType >= 0) Perm(t, EffectACIncrease(4, nACType));
    int i;
    for (i = 0; i < 8; i++) DelayCommand(0.5 + IntToFloat(i) * 0.1, Chunk(k, a, t, bRanged, 500));
    DelayCommand(1.5, TouchReport(k));
    DelayCommand(1.6, DestroyObject(a));
    DelayCommand(1.6, DestroyObject(t));
}

void Touches()
{
    Touch("base", -1, -1);
    Touch("attacker.tiny", TINY, -1);
    Touch("attacker.small", SMALL, -1);
    Touch("attacker.huge", HUGE, -1);
    Touch("target.tiny", -1, TINY);
    Touch("target.small", -1, SMALL);
    Touch("target.large", -1, LARGE);
    Touch("target.huge", -1, HUGE);
    Touch("ranged", -1, -1, TRUE);
    Touch("ranged.attacker.huge", HUGE, -1, TRUE);
    Touch("ranged.target.tiny", -1, TINY, TRUE);
    Touch("str4", -1, -1, FALSE, 4);
    Touch("dex4", -1, -1, FALSE, 0, 4);
    Touch("ranged.dex4", -1, -1, TRUE, 0, 4);
    Touch("ranged.str4", -1, -1, TRUE, 4);
    Touch("attack5", -1, -1, FALSE, 0, 0, 5);
    // Touch AC: deflection counts, dodge too but not while flat-footed; armour, natural armour and shields don't.
    Touch("ac.dodge", -1, -1, FALSE, 0, 0, 0, AC_DODGE_BONUS);
    Touch("ac.natural", -1, -1, FALSE, 0, 0, 0, AC_NATURAL_BONUS);
    Touch("ac.armour", -1, -1, FALSE, 0, 0, 0, AC_ARMOUR_ENCHANTMENT_BONUS);
    Touch("ac.shield", -1, -1, FALSE, 0, 0, 0, AC_SHIELD_ENCHANTMENT_BONUS);
    Touch("ac.deflection", -1, -1, FALSE, 0, 0, 0, AC_DEFLECTION_BONUS);
}

// ---------------------------------------------------------------- weapon attacks

void Resize(object t, int nApp)
{
    object o = GetFirstObjectInArea(GetArea(t));
    while (GetIsObjectValid(o))
    {
        if (GetLocalObject(o, "T") == t) SetCreatureAppearanceType(o, nApp);
        o = GetNextObjectInArea(GetArea(t));
    }
}

object Squad(string g, int nAttApp, int nTgtApp)
{
    object t = PSquad(g, FIGHTER, TARGET, 4);
    PSquadAttacks(t, 1);
    PSquadEffect(t, EffectModifyAttacks(2));
    Perm(t, EffectACIncrease(13, AC_DEFLECTION_BONUS));
    if (nAttApp >= 0) Resize(t, nAttApp);
    if (nTgtApp >= 0) SetCreatureAppearanceType(t, nTgtApp);
    return t;
}

void Squads()
{
    int i;
    for (i = 0; i < 5; i++)
    {
        Squad("weapon.medium", -1, -1);
        Squad("weapon.attacker.tiny", TINY, -1);
        Squad("weapon.attacker.huge", HUGE, -1);
        Squad("weapon.target.tiny", -1, TINY);
        Squad("weapon.target.huge", -1, HUGE);
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "weapon.medium weapon.attacker.tiny weapon.attacker.huge weapon.target.tiny weapon.target.huge ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        PReportHits(GetStringLeft(s, sp));
        s = GetSubString(s, sp + 1, 1000);
    }
    PDone();
}

void main()
{
    PClockStart(85.0);
    DelayCommand(1.0, Touches());
    DelayCommand(4.0, Squads());
    DelayCommand(72.0, Report());
}
