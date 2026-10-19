// Sneak attacks: on a target attacking nobody (even with Uncanny Dodge), one attacking someone else,
// but not one attacking you; not on undead, nor on targets immune to sneak attacks or to critical hits
// (which still take critical hits when only sneak-immune); Improved Uncanny Dodge stops the flanking
// kind; ranged only within 10 metres. nw_drowfight010 gets Sneak Attack 10d6 as a bonus feat; with Str 29
// and a dagger a hit deals 10-13 (20-26 critical), and 27 or more with sneak attack dice.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";
const int SNEAK_ATTACK_10 = 353;

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

void Prepare(object a, string g, string sWeapon)
{
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(2));
    Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
    Perm(a, EffectAttackIncrease(20));
    Perm(a, EffectBonusFeat(SNEAK_ATTACK_10));
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND);
    if (sWeapon == "nw_wbwln001") PEquip(a, "nw_wamar001", INVENTORY_SLOT_ARROWS, 99);
    SetLocalInt(GetModule(), g + ".critat", sWeapon == "nw_wbwln001" ? 25 : 27);
}

object Pair(string g, string sTarget = TARGET, string sWeapon = "nw_wswdg001", float fDist = 1.5)
{
    object a = PPair(g, FIGHTER, sTarget, fDist);
    Prepare(a, g, sWeapon);
    return a;
}

object Squad(string g)
{
    object t = PSquad(g, FIGHTER, TARGET, 3);
    object o = GetFirstObjectInArea(GetArea(t));
    while (GetIsObjectValid(o))
    {
        if (GetLocalObject(o, "T") == t) Prepare(o, g, "nw_wswdg001");
        o = GetNextObjectInArea(GetArea(t));
    }
    return t;
}

// The target fights a passive dummy beside it.
void FightDummy(object t)
{
    vector v = GetPosition(t);
    object d = CreateObject(OBJECT_TYPE_CREATURE, TARGET, Location(GetArea(t), Vector(v.x + 1.2, v.y + 1.2, 0.0), 0.0));
    PStrip(d);
    SetImmortal(d, TRUE);
    ChangeToStandardFaction(d, STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(t, STANDARD_FACTION_DEFENDER);
    DelayCommand(0.5, AssignCommand(t, ActionAttack(d)));
}

void Setup()
{
    int i;
    for (i = 0; i < 2; i++)
    {
        Squad("flatfooted");
        Perm(Squad("uncanny"), EffectBonusFeat(FEAT_UNCANNY_DODGE_1));
        Perm(Squad("immune"), EffectImmunity(IMMUNITY_TYPE_SNEAK_ATTACK));
        Perm(Squad("critimmune"), EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
        FightDummy(Squad("flanked"));
        object t = Squad("flanked.improveduncanny");
        Perm(t, EffectBonusFeat(FEAT_UNCANNY_DODGE_2));
        FightDummy(t);
        object a = Pair("undead", "nw_skeleton");
        a = Pair("bow.8m", TARGET, "nw_wbwln001", 8.0);
        a = Pair("bow.14m", TARGET, "nw_wbwln001", 14.0);
    }
    for (i = 0; i < 4; i++)
    {
        object a = Pair("fighting");
        object t = GetLocalObject(a, "T");
        ChangeToStandardFaction(t, STANDARD_FACTION_DEFENDER);
        DelayCommand(0.5, AssignCommand(t, ActionAttack(a)));
    }
    // The attackers start once the targets that fight are under way, so those are never caught flat-footed.
    PStartAll(3.0);
}

void Report()
{
    // Sneak-immune targets still take critical hits (20-26).
    int nCrit = 0, i;
    for (i = 20; i <= 26; i++) nCrit += PGet("immune.d" + IntToString(i));
    PLogRate("immune.crits", nCrit, PGet("immune.hit"));
    string s = "flatfooted uncanny immune critimmune flanked flanked.improveduncanny undead fighting bow.8m bow.14m ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PLogRate(g + ".sneak", PGet(g + ".crit"), PGet(g + ".hit"));
        int n = PGet(g + ".crit");
        // (nwserver lets a few percent through on targets that fight: brief moments their attack lapses.)
        if (n > 0 && g != "flanked.improveduncanny" && g != "fighting") PLogMean(g + ".sneak.mean", IntToFloat(PGet(g + ".critsum")), n, PSd(PGet(g + ".critsum"), PGet(g + ".critsq"), n), 0.5);
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
