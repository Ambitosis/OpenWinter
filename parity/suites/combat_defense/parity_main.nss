// Defences: concealment and miss chance (with Blind-Fight), damage reduction, resistance, immunity
// and vulnerability, and how damage effects meet them: physical ones join the weapon's damage, others
// get past immunity and resistance; item property damage doesn't. A type that was fully resisted is
// still reported, as 0. A plot target takes 0 (OnDamaged still runs). nw_drowfight010 with Str 29 and a dagger (10-13 damage) attacks targets immune to
// critical hits, six times a round with +20 attack, so hits only fail to concealment and damage is exact.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Pair(string g, string sWeapon = "nw_wswdg001", int nAttack = 20)
{
    object a = PPair(g, FIGHTER, TARGET);
    object t = GetLocalObject(a, "T");
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(5));
    Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
    if (nAttack > 0) Perm(a, EffectAttackIncrease(nAttack));
    Perm(t, EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    SetLocalObject(a, "W", PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND));
    return a;
}

object T(object a)
{
    return GetLocalObject(a, "T");
}

void Setup()
{
    int i;
    for (i = 0; i < 2; i++)
    {
        // Miss chances.
        Pair("plain");
        Perm(T(Pair("conceal50")), EffectConcealment(50));
        Perm(T(Pair("conceal50.ranged")), EffectConcealment(50, MISS_CHANCE_TYPE_VS_RANGED));
        object a = Pair("conceal50.blindfight");
        Perm(T(a), EffectConcealment(50));
        Perm(a, EffectBonusFeat(FEAT_BLIND_FIGHT));
        Perm(Pair("misschance30"), EffectMissChance(30));
        a = Pair("conceal20.miss40");
        Perm(T(a), EffectConcealment(20));
        Perm(a, EffectMissChance(40));
        // Damage reduction 5/+1: a plain dagger is soaked, a +2 one isn't; a 10-point limit runs out.
        Perm(T(Pair("dr5")), EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE));
        a = Pair("dr5.enh2");
        Perm(T(a), EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE));
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(2), GetLocalObject(a, "W"));
        Perm(T(Pair("dr5.limit")), EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE, 10));
        // Resistance 5 to piercing (the best of two), and to fire against +1d6 fire.
        a = Pair("resist5");
        Perm(T(a), EffectDamageResistance(DAMAGE_TYPE_PIERCING, 5));
        Perm(T(a), EffectDamageResistance(DAMAGE_TYPE_PIERCING, 3));
        Perm(T(Pair("resist.slashing")), EffectDamageResistance(DAMAGE_TYPE_SLASHING, 5));
        a = Pair("resist.fire");
        Perm(a, EffectDamageIncrease(DAMAGE_BONUS_1d6, DAMAGE_TYPE_FIRE));
        Perm(T(a), EffectDamageResistance(DAMAGE_TYPE_FIRE, 3));
        a = Pair("immune.fire");
        Perm(a, EffectDamageIncrease(DAMAGE_BONUS_1d6, DAMAGE_TYPE_FIRE));
        Perm(T(a), EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE, 100));
        a = Pair("ip.resist.fire");
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6), GetLocalObject(a, "W"));
        Perm(T(a), EffectDamageResistance(DAMAGE_TYPE_FIRE, 3));
        a = Pair("ip.vulnerable.fire");
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_5), GetLocalObject(a, "W"));
        Perm(T(a), EffectDamageImmunityDecrease(DAMAGE_TYPE_FIRE, 50));
        // +5 slashing on a piercing dagger joins the dagger's damage: slashing resistance doesn't stop it.
        a = Pair("slashing5.resist.slashing");
        Perm(a, EffectDamageIncrease(DAMAGE_BONUS_5, DAMAGE_TYPE_SLASHING));
        Perm(T(a), EffectDamageResistance(DAMAGE_TYPE_SLASHING, 5));
        a = Pair("piercing5.resist.piercing");
        Perm(a, EffectDamageIncrease(DAMAGE_BONUS_5, DAMAGE_TYPE_PIERCING));
        Perm(T(a), EffectDamageResistance(DAMAGE_TYPE_PIERCING, 5));
        // A plot target: every hit still runs OnDamaged, reporting 0 base weapon damage.
        a = Pair("plot");
        SetPlotFlag(T(a), TRUE);
        // Damage reduction soaking the whole weapon hit: only the fire gets through.
        a = Pair("dr20.fire");
        Perm(a, EffectDamageIncrease(DAMAGE_BONUS_5, DAMAGE_TYPE_FIRE));
        Perm(T(a), EffectDamageReduction(20, DAMAGE_POWER_PLUS_ONE));
        // Immunity and vulnerability by percentage.
        Perm(T(Pair("immune50")), EffectDamageImmunityIncrease(DAMAGE_TYPE_PIERCING, 50));
        Perm(T(Pair("immune25")), EffectDamageImmunityIncrease(DAMAGE_TYPE_PIERCING, 25));
        Perm(T(Pair("vulnerable50")), EffectDamageImmunityDecrease(DAMAGE_TYPE_PIERCING, 50));
        a = Pair("immune50.resist5");
        Perm(T(a), EffectDamageImmunityIncrease(DAMAGE_TYPE_PIERCING, 50));
        Perm(T(a), EffectDamageResistance(DAMAGE_TYPE_PIERCING, 5));
        a = Pair("immune50.dr5");
        Perm(T(a), EffectDamageImmunityIncrease(DAMAGE_TYPE_PIERCING, 50));
        Perm(T(a), EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE));
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "plain conceal50 conceal50.ranged conceal50.blindfight misschance30 conceal20.miss40 ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        PReportHits(GetStringLeft(s, sp));
        s = GetSubString(s, sp + 1, 1000);
    }
    s = "plain dr5 dr5.enh2 resist5 resist.slashing immune50 immune25 vulnerable50 immune50.resist5 immune50.dr5 "
        + "resist.fire immune.fire ip.resist.fire ip.vulnerable.fire slashing5.resist.slashing piercing5.resist.piercing dr20.fire ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        PReportDamage(GetStringLeft(s, sp));
        s = GetSubString(s, sp + 1, 1000);
    }
    // The limited reduction soaks 10 points, then the rest gets through.
    PLogI("dr5.limit.hits", PGet("dr5.limit.hit") >= 4);
    PLogI("dr5.limit.max", PGet("dr5.limit.dmax"));
    s = "resist.fire immune.fire ip.resist.fire ip.vulnerable.fire dr20.fire ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        int n = PGet(g + ".firen");
        PLogRate(g + ".fire.reported", n, PGet(g + ".hit"));
        PLogI(g + ".fire.min", PGet(g + ".firemin"));
        PLogI(g + ".fire.max", PGet(g + ".firemax"));
        PLogMean(g + ".fire.mean", IntToFloat(PGet(g + ".fire")), n, PSd(PGet(g + ".fire"), PGet(g + ".firesq"), n), 0.05);
        s = GetSubString(s, sp + 1, 1000);
    }
    PLogRate("dr20.fire.weapon.reported", PGet("dr20.fire.dbase"), PGet("dr20.fire.hit"));
    PLogRate("plot.damaged.events.per.attack", PGet("plot.hit"), PGet("plot.atk"));
    PLogI("plot.damage.total", PGet("plot.dmg"));
    PLogRate("plot.base.reported", PGet("plot.dbase"), PGet("plot.hit"));
    PDone();
}

void main()
{
    PClockStart(85.0);
    DelayCommand(1.0, Setup());
    DelayCommand(70.0, Report());
}
