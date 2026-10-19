// Critical hits: threat ranges by weapon, Keen and Improved Critical (they stack), multipliers,
// Massive Criticals, confirmation rolls (a natural 1 doesn't fail one) and immunity. nw_drowfight010 with Str 29 attacks
// six times a round with +20 attack against a flat-footed nw_bandit001, so nearly every attack hits and
// confirms; with Str +9 a critical hit's damage is always above a normal hit's, which tells them apart.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Pair(string g, string sWeapon, int nCritAt, int nAttack = 20)
{
    object a = PPair(g, FIGHTER, TARGET);
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(5));
    Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
    if (nAttack > 0) Perm(a, EffectAttackIncrease(nAttack));
    if (nAttack < 0) Perm(a, EffectAttackDecrease(-nAttack));
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    if (sWeapon != "") SetLocalObject(a, "W", PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND));
    SetLocalInt(GetModule(), g + ".critat", nCritAt);
    return a;
}

void Keen(object a)
{
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), GetLocalObject(a, "W"));
}

void Setup()
{
    int i;
    for (i = 0; i < 2; i++)
    {
        // Normal hits deal 10-15 (rapier 1d6+9), 10-17 (longsword), 14-25 (greataxe 1d12+13), 15-21 (scythe 2d4+13),
        // 10-12 (unarmed 1d3+9), 10-13 (dagger).
        Pair("rapier", "nw_wswrp001", 16);
        Pair("longsword", "nw_wswls001", 18);
        Pair("greataxe", "nw_waxgr001", 26);
        Pair("scythe", "nw_wplsc001", 22);
        Pair("unarmed", "", 13);
        object a = Pair("massive", "nw_wswdg001", 14);
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyMassiveCritical(IP_CONST_DAMAGEBONUS_2d6), GetLocalObject(a, "W"));
        // Immune targets: by effect, and undead.
        a = Pair("immune", "nw_wswls001", 18);
        Perm(GetLocalObject(a, "T"), EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
        // Confirmation: +11 against AC 11 hits and confirms 55% of the time.
        Pair("confirm", "nw_wswrp001", 16, -12);
    }
    for (i = 0; i < 7; i++)
    {
        object a = Pair("keen.impcrit", "nw_wswls001", 18);
        Keen(a);
        Perm(a, EffectBonusFeat(FEAT_IMPROVED_CRITICAL_LONG_SWORD));
    }
    for (i = 0; i < 3; i++)
    {
        object a = Pair("keen", "nw_wswls001", 18);
        Keen(a);
        a = Pair("impcrit", "nw_wswls001", 18);
        Perm(a, EffectBonusFeat(FEAT_IMPROVED_CRITICAL_LONG_SWORD));
    }
    for (i = 0; i < 2; i++)
    {
        object a = PPair("undead", FIGHTER, "nw_skeleton");
        SetBaseAttackBonus(1, a);
        Perm(a, EffectModifyAttacks(5));
        Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
        Perm(a, EffectAttackIncrease(20));
        PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
        PEquip(a, "nw_wswls001", INVENTORY_SLOT_RIGHTHAND);
        SetLocalInt(GetModule(), "undead.critat", 18);
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "rapier longsword greataxe scythe unarmed massive immune confirm keen impcrit keen.impcrit undead ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PLogRate(g + ".critrate", PGet(g + ".crit"), PGet(g + ".atk"), 0.01);
        int n = PGet(g + ".crit");
        if (n > 0) PLogMean(g + ".critmean", IntToFloat(PGet(g + ".critsum")), n, PSd(PGet(g + ".critsum"), PGet(g + ".critsq"), n), 0.1);
        s = GetSubString(s, sp + 1, 1000);
    }
    PReportHits("confirm");
    PDone();
}

void main()
{
    PClockStart(85.0);
    DelayCommand(1.0, Setup());
    DelayCommand(70.0, Report());
}
