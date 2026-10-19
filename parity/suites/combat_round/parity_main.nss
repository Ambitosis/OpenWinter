// Combat rounds: attacks per round (BAB, SetBaseAttackBonus, haste, extra attacks, slow, two weapons,
// monks with and without a haste item, bows), round length, OnEndCombatRound for defenders,
// OnPhysicalAttacked per attack, passive creatures not fighting back, and how weapon damage is typed.
// (When attacks fall inside the round is too noisy on nwserver to compare; see AttackTimes in OpenWinter.)
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

object Pair(string g, string sAttacker = FIGHTER, float fDist = 1.5)
{
    return PPair(g, sAttacker, TARGET, fDist);
}

void Effect(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

void Setup()
{
    int i, n;
    for (n = 1; n <= 6; n++)
        for (i = 0; i < 2; i++) SetBaseAttackBonus(n, Pair("sb" + IntToString(n)));
    for (i = 0; i < 2; i++)
    {
        Pair("bab10");
        Pair("bab15", "nw_drowfight015");
        Pair("bab20", "nw_drowfight020");
        object o = Pair("haste");
        SetBaseAttackBonus(1, o);
        Effect(o, EffectHaste());
        o = Pair("extra2");
        SetBaseAttackBonus(1, o);
        Effect(o, EffectModifyAttacks(2));
        o = Pair("slow", "nw_drowfight015");
        Effect(o, EffectSlow());
        o = Pair("dual");
        PEquip(o, "nw_wswss001", INVENTORY_SLOT_LEFTHAND);
        // Grimgnaw (monk 14) wears a robe with the Haste property; without it he has his base attacks.
        o = Pair("monk", "nw_hen_gri_11");
        o = Pair("monknorobe", "nw_hen_gri_11");
        PUnequip(o, INVENTORY_SLOT_CHEST);
        o = Pair("monk8", "nw_hen_gri_05");
        o = Pair("bow", FIGHTER, 8.0);
        PUnequip(o, INVENTORY_SLOT_RIGHTHAND);
        PEquip(o, "nw_wbwln001", INVENTORY_SLOT_RIGHTHAND);
        PEquip(o, "nw_wamar001", INVENTORY_SLOT_ARROWS, 99);
        // A target with no scripts at all.
        o = Pair("bare");
        object t = GetLocalObject(o, "T");
        int ev;
        for (ev = 5000; ev <= 5012; ev++) SetEventScript(t, ev, "");
    }
}

void Apr(string sResRef)
{
    object o = PSpawn(sResRef, 75.0, 75.0);
    PLogI("apr." + sResRef, GetAttacksPerRound(o));
    PLogI("bab." + sResRef, GetBaseAttackBonus(o));
    SetBaseAttackBonus(5, o);
    PLogI("apr.set5." + sResRef, GetAttacksPerRound(o));
    PLogI("apr.raw." + sResRef, GetAttacksPerRound(o, FALSE));
    DestroyObject(o);
}

void ReportGroup(string g)
{
    PReportRounds(g);
    PLogMean(g + ".defender.round", IntToFloat(PGet(g + ".trlen")) / 20.0, PGet(g + ".trounds"), 0.05, 0.1);
    PLogI(g + ".foughtback", PGet(g + ".back") > 0);
}

void Report()
{
    string s = "sb1 sb2 sb3 sb4 sb5 sb6 bab10 bab15 bab20 haste extra2 slow dual monk monknorobe monk8 bow bare ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        ReportGroup(GetStringLeft(s, sp));
        s = GetSubString(s, sp + 1, 1000);
    }
    // Weapon damage is reported as base weapon damage, not by physical type.
    PLogI("dmgtype.base", PGet("bab10.dbase") == PGet("bab10.hit"));
    PLogI("dmgtype.physical", PGet("bab10.dphys") > 0);
    PLogI("dmgtype.bow.base", PGet("bow.dbase") == PGet("bow.hit"));
    PDone();
}

void Go()
{
    Apr("nw_drowfight001");
    Apr("nw_drowfight005");
    Apr("nw_drowfight010");
    Apr("nw_drowfight015");
    Apr("nw_drowfight020");
    Apr("nw_dwarfmerc005");
    Apr("nw_dwarfmerc006");
    Apr("x2_mephdrow006");
    Apr("nw_hen_gri_11");
    Apr("nw_curst004");
    PClockStart(85.0);
    Setup();
    PStartAll(1.0);
    DelayCommand(66.0, Report());
}

void main()
{
    DelayCommand(2.0, Go());
}
