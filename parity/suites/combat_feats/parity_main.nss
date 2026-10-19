// Weapon feats and proficiency: (Epic) Weapon Focus and Specialization given as bonus feats, the -4
// for a weapon the creature isn't proficient with (a commoner with martial and exotic weapons), and
// monk unarmed damage by level (Grimgnaw without his gloves and robe). Targets are flat-footed
// nw_bandit001 immune to critical hits.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Pair(string g, string sAttacker, string sWeapon, int nDefl)
{
    object a = PPair(g, sAttacker, TARGET);
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(1));
    object t = GetLocalObject(a, "T");
    if (nDefl > 0) Perm(t, EffectACIncrease(nDefl, AC_DEFLECTION_BONUS));
    Perm(t, EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
    if (sWeapon != "keep")
    {
        PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
        PUnequip(a, INVENTORY_SLOT_ARMS);
        PUnequip(a, INVENTORY_SLOT_CHEST);
        if (sWeapon != "") PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND);
    }
    return a;
}

void Setup()
{
    int i;
    for (i = 0; i < 3; i++)
    {
        Pair("longsword", FIGHTER, "nw_wswls001", 13);
        object a = Pair("longsword.focus", FIGHTER, "nw_wswls001", 13);
        Perm(a, EffectBonusFeat(FEAT_WEAPON_FOCUS_LONG_SWORD));
        Perm(a, EffectBonusFeat(FEAT_EPIC_WEAPON_FOCUS_LONGSWORD));
        a = Pair("longsword.spec", FIGHTER, "nw_wswls001", 13);
        Perm(a, EffectBonusFeat(FEAT_WEAPON_SPECIALIZATION_LONG_SWORD));
        Perm(a, EffectBonusFeat(FEAT_EPIC_WEAPON_SPECIALIZATION_LONGSWORD));
        Pair("commoner.club", "nw_commale", "nw_wblcl001", 0);
        Pair("commoner.longsword", "nw_commale", "nw_wswls001", 0);
        Pair("commoner.bastard", "nw_commale", "nw_wswbs001", 0);
        Pair("monk4", "nw_hen_gri_01", "", 0);
        Pair("monk8", "nw_hen_gri_05", "", 0);
        Pair("monk14", "nw_hen_gri_11", "", 0);
        Pair("commoner.unarmed", "nw_commale", "", 0);
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "longsword longsword.focus longsword.spec commoner.club commoner.longsword commoner.bastard ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        PReportHits(GetStringLeft(s, sp));
        s = GetSubString(s, sp + 1, 1000);
    }
    s = "longsword longsword.spec monk4 monk8 monk14 commoner.unarmed ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        PReportDamage(GetStringLeft(s, sp));
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
