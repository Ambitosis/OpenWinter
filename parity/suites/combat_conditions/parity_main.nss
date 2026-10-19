// Conditions in combat. On nwserver a paralysed, stunned, asleep or dazed target gives no bonus to hit;
// a blind one gives +2 and loses its Dexterity bonus; a prone one gives +4 in melee and loses it too;
// a blind attacker takes -4 and a 50% miss chance; darkness on the target conceals it, darkness on the
// attacker does nothing; an invisible attacker gets +2 and the target loses its Dexterity bonus.
// nw_drowfight010 (+14) attacks nw_bandit001 with Dex 23 and +13 deflection (AC 30, or 24 when it loses
// its Dexterity), fighting back or passive. (Targets paralysed before they were told to attack stay
// commandable on nwserver and so still count as fighting; OpenWinter locks them, so those aren't compared.)
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object P(string g, int bPassive = FALSE)
{
    object a = PPair(g, FIGHTER, TARGET);
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(1));
    object t = GetLocalObject(a, "T");
    Perm(t, EffectACIncrease(13, AC_DEFLECTION_BONUS));
    Perm(t, EffectAbilityIncrease(ABILITY_DEXTERITY, 10));
    if (!bPassive)
    {
        ChangeToStandardFaction(t, STANDARD_FACTION_DEFENDER);
        SetBaseAttackBonus(1, t);
        DelayCommand(0.5, AssignCommand(t, ActionAttack(a)));
    }
    return a;
}

object T(object a)
{
    return GetLocalObject(a, "T");
}

void Setup()
{
    int i;
    for (i = 0; i < 3; i++)
    {
        P("fighting");
        P("passive", TRUE);
        Perm(T(P("passive.stunned", TRUE)), EffectStunned());
        Perm(T(P("passive.asleep", TRUE)), EffectSleep());
        Perm(T(P("fighting.dazed")), EffectDazed());
        Perm(T(P("fighting.blind")), EffectBlindness());
        Perm(T(P("fighting.prone")), EffectKnockdown());
        Perm(T(P("passive.paralysed", TRUE)), EffectParalyze());
        Perm(T(P("passive.blind", TRUE)), EffectBlindness());
        Perm(P("passive.attacker.blind", TRUE), EffectBlindness());
        Perm(P("passive.attacker.darkness", TRUE), EffectDarkness());
        Perm(T(P("passive.target.darkness", TRUE)), EffectDarkness());
        Perm(P("fighting.attacker.invisible"), EffectInvisibility(INVISIBILITY_TYPE_IMPROVED));
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "fighting passive passive.stunned passive.asleep fighting.dazed fighting.blind fighting.prone "
        + "passive.paralysed passive.blind passive.attacker.blind passive.attacker.darkness passive.target.darkness fighting.attacker.invisible ";
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
    DelayCommand(1.0, Setup());
    DelayCommand(70.0, Report());
}
