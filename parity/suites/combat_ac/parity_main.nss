// Armour class and attack modifiers in combat. AC decreases come off their own type: a flat-footed
// target loses its dodge part, penalties included; others always count. Touch attacks only face
// 10 + size + deflection. Attack increases and decreases are each capped at the attack bonus limit.
// nw_drowfight010 (+14) attacks nw_bandit001 (AC 12, +13 deflection), passive (squads) or fighting back
// (pairs); touch attacks are made thousands at a time.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

// ---------------------------------------------------------------- weapon attacks

object Squad(string g)
{
    object t = PSquad(g, FIGHTER, TARGET, 4);
    PSquadAttacks(t, 1);
    PSquadEffect(t, EffectModifyAttacks(1));
    Perm(t, EffectACIncrease(13, AC_DEFLECTION_BONUS));
    return t;
}

object Fight(string g)
{
    object a = PPair(g, FIGHTER, TARGET);
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(1));
    object t = GetLocalObject(a, "T");
    Perm(t, EffectACIncrease(13, AC_DEFLECTION_BONUS));
    ChangeToStandardFaction(t, STANDARD_FACTION_DEFENDER);
    SetBaseAttackBonus(1, t);
    DelayCommand(0.5, AssignCommand(t, ActionAttack(a)));
    return t;
}

void Weapons()
{
    int i;
    for (i = 0; i < 2; i++)
    {
        Squad("passive.base");
        Perm(Squad("passive.dodge+5"), EffectACIncrease(5, AC_DODGE_BONUS));
        Perm(Squad("passive.dodge-5"), EffectACDecrease(5, AC_DODGE_BONUS));
        Perm(Squad("passive.natural+5"), EffectACIncrease(5, AC_NATURAL_BONUS));
        Perm(Squad("passive.natural-5"), EffectACDecrease(5, AC_NATURAL_BONUS));
    }
    for (i = 0; i < 6; i++)
    {
        Fight("fighting.base");
        Perm(Fight("fighting.dodge+5"), EffectACIncrease(5, AC_DODGE_BONUS));
        Perm(Fight("fighting.dodge-5"), EffectACDecrease(5, AC_DODGE_BONUS));
    }
    PStartAll(1.0);
}

// ---------------------------------------------------------------- touch attacks

void Chunk(string k, object a, object t, int n)
{
    int i;
    for (i = 0; i < n; i++) PAdd(k + ".r" + IntToString(TouchAttackMelee(t, FALSE, a)));
}

void TouchReport(string k)
{
    PLogRate("touch." + k, PGet(k + ".r1") + PGet(k + ".r2"), PGet(k + ".r0") + PGet(k + ".r1") + PGet(k + ".r2"));
}

// 4000 touch attacks (+13) on a target with +10 deflection (or none), passive or fighting back.
object Touch(string k, int bFight, int nAttackInc = 0, int nAttackDec = 0, int nDeflection = 10)
{
    int n = PGet("PAIRS");
    PAdd("PAIRS");
    object a = CreateObject(OBJECT_TYPE_CREATURE, FIGHTER, PCell(n, 0.0));
    object t = CreateObject(OBJECT_TYPE_CREATURE, TARGET, PCell(n, 1.5));
    PStrip(a);
    PStrip(t);
    SetImmortal(a, TRUE);
    ChangeToStandardFaction(a, STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(t, STANDARD_FACTION_DEFENDER);
    if (nDeflection > 0) Perm(t, EffectACIncrease(nDeflection, AC_DEFLECTION_BONUS));
    if (nAttackInc > 0) Perm(a, EffectAttackIncrease(nAttackInc));
    if (nAttackDec > 0) Perm(a, EffectAttackDecrease(nAttackDec));
    if (bFight) DelayCommand(0.3, AssignCommand(t, ActionAttack(a)));
    int i;
    for (i = 0; i < 8; i++) DelayCommand(2.0 + IntToFloat(i) * 0.1, Chunk(k, a, t, 500));
    DelayCommand(3.0, TouchReport(k));
    DelayCommand(3.1, DestroyObject(a));
    DelayCommand(3.1, DestroyObject(t));
    return t;
}

void Touches()
{
    int f;
    for (f = 0; f <= 1; f++)
    {
        string p = f ? "fighting." : "passive.";
        Touch(p + "base", f);
        Perm(Touch(p + "dodge+4", f), EffectACIncrease(4, AC_DODGE_BONUS));
        Perm(Touch(p + "dex+10", f), EffectAbilityIncrease(ABILITY_DEXTERITY, 10));
        Perm(Touch(p + "deflection-4", f), EffectACDecrease(4, AC_DEFLECTION_BONUS));
        Perm(Touch(p + "dodge-4", f), EffectACDecrease(4, AC_DODGE_BONUS));
        Perm(Touch(p + "natural-4", f), EffectACDecrease(4, AC_NATURAL_BONUS));
        Perm(Touch(p + "armour-4", f), EffectACDecrease(4, AC_ARMOUR_ENCHANTMENT_BONUS));
    }
    Perm(Touch("deflection+25", FALSE), EffectACIncrease(25, AC_DEFLECTION_BONUS));
    // Attack modifiers: increases and decreases each capped at 20.
    Touch("attack-10", FALSE, 0, 10);
    Touch("attack-30", FALSE, 0, 30, 0);
    Touch("attack+10-30", FALSE, 10, 30);
    Touch("attack+20-30", FALSE, 20, 30);
    Touch("attack+5-10", FALSE, 5, 10);
}

void Report()
{
    string s = "passive.base passive.dodge+5 passive.dodge-5 passive.natural+5 passive.natural-5 fighting.base fighting.dodge+5 fighting.dodge-5 ";
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
    DelayCommand(5.0, Weapons());
    DelayCommand(72.0, Report());
}
