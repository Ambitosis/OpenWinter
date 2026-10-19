// Two weapons and Weapon Finesse. Each hand's hits are told apart by damage: the main hand is a +5
// longsword (15-22 with Str 29), the off hand a dagger or longsword (half Strength: 5-8 or 5-12).
// nw_drowfight010 makes one main-hand attack a round, against a flat-footed nw_bandit001 (AC 11) given
// deflection. Off-hand penalties: -6/-10, lighter with a light off-hand weapon, Two-Weapon Fighting and
// Ambidexterity; Improved Two-Weapon Fighting adds an off-hand attack at -5.
// Weapon Finesse uses Dexterity for light weapons and rapiers.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Dual(string g, string sOff, int nAC)
{
    object a = PPair(g, FIGHTER, TARGET);
    SetBaseAttackBonus(1, a);
    Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
    Perm(GetLocalObject(a, "T"), EffectACIncrease(nAC, AC_DEFLECTION_BONUS));
    Perm(GetLocalObject(a, "T"), EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    object w = PEquip(a, "nw_wswls001", INVENTORY_SLOT_RIGHTHAND);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(5), w);
    PEquip(a, sOff, INVENTORY_SLOT_LEFTHAND);
    SetLocalInt(GetModule(), g + ".winlo", 1);
    SetLocalInt(GetModule(), g + ".winhi", 14);
    return a;
}

object Finesse(string g, string sWeapon, int bFeat)
{
    object a = PPair(g, FIGHTER, TARGET);
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(1));
    Perm(a, EffectAbilityIncrease(ABILITY_DEXTERITY, 10));
    if (bFeat) Perm(a, EffectBonusFeat(FEAT_WEAPON_FINESSE));
    Perm(GetLocalObject(a, "T"), EffectACIncrease(16, AC_DEFLECTION_BONUS));
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND);
    return a;
}

void Setup()
{
    int i;
    for (i = 0; i < 3; i++)
    {
        // Main hand +24 with the +5 longsword, off hand +19, less the two-weapon penalties, against AC 21.
        Dual("heavy", "nw_wswls001", 10);
        Dual("light", "nw_wswdg001", 10);
        Perm(Dual("light.twf", "nw_wswdg001", 10), EffectBonusFeat(FEAT_TWO_WEAPON_FIGHTING));
        object a = Dual("light.twf.ambi", "nw_wswdg001", 10);
        Perm(a, EffectBonusFeat(FEAT_TWO_WEAPON_FIGHTING));
        Perm(a, EffectBonusFeat(FEAT_AMBIDEXTERITY));
        a = Dual("light.itwf", "nw_wswdg001", 10);
        Perm(a, EffectBonusFeat(FEAT_TWO_WEAPON_FIGHTING));
        Perm(a, EffectBonusFeat(FEAT_AMBIDEXTERITY));
        Perm(a, EffectBonusFeat(FEAT_IMPROVED_TWO_WEAPON_FIGHTING));
        Finesse("finesse.dagger", "nw_wswdg001", TRUE);
        Finesse("finesse.rapier", "nw_wswrp001", TRUE);
        Finesse("finesse.longsword", "nw_wswls001", TRUE);
        Finesse("strength.dagger", "nw_wswdg001", FALSE);
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "heavy light light.twf light.twf.ambi light.itwf ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PReportRounds(g);
        // Hits per round by hand (the off hand's damage is in the window).
        PLogMean(g + ".offhand.hits", IntToFloat(PGet(g + ".win")), PGet(g + ".rounds"), 0.5, 0.05);
        PLogMean(g + ".mainhand.hits", IntToFloat(PGet(g + ".hit") - PGet(g + ".win")), PGet(g + ".rounds"), 0.5, 0.05);
        if (g != "heavy") PLogI(g + ".offhand.min", PGet(g + ".winmin"));
        s = GetSubString(s, sp + 1, 1000);
    }
    s = "finesse.dagger finesse.rapier finesse.longsword strength.dagger ";
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
