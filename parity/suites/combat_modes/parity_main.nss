// SetActionMode on scripted NPCs: does the mode take (GetActionMode) and does it change the fight? nw_drowfight010 with
// Power Attack (-5 to hit, +5 damage) or Improved Power Attack (-10, +10) attacks a flat-footed bandit; with Expertise
// (+5 AC) or Improved Expertise (+10) it is attacked. Each with the mode set and without (control).
#include "pcombat_inc"

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

// The attacker of a pair, with nFeat and nMode set (or not).
object Attacker(string g, int nFeat, int nMode, int bSet)
{
    object a = PPair(g, "nw_drowfight010", "nw_bandit001");
    SetBaseAttackBonus(1, a);
    Perm(GetLocalObject(a, "T"), EffectACIncrease(4, AC_DEFLECTION_BONUS));
    Perm(GetLocalObject(a, "T"), EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
    if (nFeat >= 0) Perm(a, EffectBonusFeat(nFeat));
    if (bSet) DelayCommand(0.5, SetActionMode(a, nMode, TRUE));
    SetLocalInt(a, "MODE", nMode);
    return a;
}

// A pair whose target (the drow) defends in a mode: the bandit attacks it.
object Defender(string g, int nFeat, int nMode, int bSet)
{
    object a = PPair(g, "nw_bandit001", "nw_drowfight010");
    SetBaseAttackBonus(1, a);
    Perm(a, EffectAttackIncrease(12));
    object t = GetLocalObject(a, "T");
    Perm(t, EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
    if (nFeat >= 0) Perm(t, EffectBonusFeat(nFeat));
    if (bSet) DelayCommand(0.5, SetActionMode(t, nMode, TRUE));
    SetLocalInt(a, "MODE", nMode);
    SetLocalInt(a, "DEF", TRUE);
    return a;
}

void ModeCheck(string sKey, string sGroup = "")
{
    object o = GetFirstObjectInArea(GetObjectByTag("PARITY_AREA"));
    int n = 0, on = 0;
    while (GetIsObjectValid(o))
    {
        string g = GetLocalString(o, "G");
        if ((sGroup == "" ? GetStringLeft(g, 3) == "set" : g == sGroup) && GetIsObjectValid(GetLocalObject(o, "T")))
        {
            object who = GetLocalInt(o, "DEF") ? GetLocalObject(o, "T") : o;
            n++;
            on += GetActionMode(who, GetLocalInt(o, "MODE"));
        }
        o = GetNextObjectInArea(GetObjectByTag("PARITY_AREA"));
    }
    PLogI(sKey + ".modes.on", on);
    PLogI(sKey + ".modes.set", n);
}

void Setup()
{
    int i;
    for (i = 0; i < 3; i++)
    {
        Attacker("set.pa", FEAT_POWER_ATTACK, ACTION_MODE_POWER_ATTACK, TRUE);
        Attacker("set.ipa", FEAT_IMPROVED_POWER_ATTACK, ACTION_MODE_IMPROVED_POWER_ATTACK, TRUE);
        Attacker("plain.pa", FEAT_POWER_ATTACK, ACTION_MODE_POWER_ATTACK, FALSE);
        Defender("set.exp", FEAT_EXPERTISE, ACTION_MODE_EXPERTISE, TRUE);
        Defender("set.iexp", FEAT_IMPROVED_EXPERTISE, ACTION_MODE_IMPROVED_EXPERTISE, TRUE);
        Defender("plain.exp", FEAT_EXPERTISE, ACTION_MODE_EXPERTISE, FALSE);
        // Attackers in a defensive mode, and other attack modes: do they stay on?
        Attacker("set.atk.exp", FEAT_EXPERTISE, ACTION_MODE_EXPERTISE, TRUE);
        Attacker("set.atk.flurry", FEAT_FLURRY_OF_BLOWS, ACTION_MODE_FLURRY_OF_BLOWS, TRUE);
        Attacker("set.atk.parry", -1, ACTION_MODE_PARRY, TRUE);
    }
    DelayCommand(1.0, ModeCheck("t1"));
    DelayCommand(30.0, ModeCheck("t30"));
    string s = "set.pa set.ipa set.exp set.iexp set.atk.exp set.atk.flurry set.atk.parry ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        DelayCommand(1.4, ModeCheck(g + ".t0", g));
        DelayCommand(2.6, ModeCheck(g + ".t1", g));
        DelayCommand(4.0, ModeCheck(g + ".t2", g));
        DelayCommand(8.0, ModeCheck(g + ".t6", g));
        s = GetSubString(s, sp + 1, 1000);
    }
    PStartAll(1.5);
}

void Report()
{
    string s = "set.pa set.ipa plain.pa set.exp set.iexp plain.exp ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PReportHits(g);
        int n = PGet(g + ".hit");
        PLogMean(g + ".mean", IntToFloat(PGet(g + ".dmg")), n, PSd(PGet(g + ".dmg"), PGet(g + ".dsq"), n), 0.05);
        s = GetSubString(s, sp + 1, 1000);
    }
    PDone();
}

void main()
{
    PClockStart(70.0);
    DelayCommand(1.0, Setup());
    DelayCommand(60.0, Report());
}
