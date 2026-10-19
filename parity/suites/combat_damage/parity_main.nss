// Weapon damage: the dice of each weapon, Strength (one-handed, two-handed, off hand, penalties), damage
// effects and weapon properties, against a target immune to critical hits. nw_drowfight010 (Str 17,
// specialised in the bastard sword only) attacks six times a round. Smallest and largest damage are
// compared only where they are common enough to always turn up.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Pair(string g, string sMain, string sOff = "", int nStr = 0)
{
    object a = PPair(g, FIGHTER, TARGET);
    object t = GetLocalObject(a, "T");
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(5));
    Perm(t, EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
    if (sMain != "default")
    {
        PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
        if (sMain != "") PEquip(a, sMain, INVENTORY_SLOT_RIGHTHAND);
    }
    if (sOff != "") PEquip(a, sOff, INVENTORY_SLOT_LEFTHAND);
    if (nStr > 0) Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, nStr));
    if (nStr < 0) Perm(a, EffectAbilityDecrease(ABILITY_STRENGTH, -nStr));
    return a;
}

void Group(string g, string sMain, string sOff = "", int nStr = 0)
{
    Pair(g, sMain, sOff, nStr);
    Pair(g, sMain, sOff, nStr);
}

void Window(string g, int nLo, int nHi)
{
    SetLocalInt(GetModule(), g + ".winlo", nLo);
    SetLocalInt(GetModule(), g + ".winhi", nHi);
}

void Setup()
{
    // Dice (+3 Strength).
    Group("dagger", "nw_wswdg001");
    Group("longsword", "nw_wswls001");
    Group("greataxe", "nw_waxgr001");
    Group("greatsword", "nw_wswgs001");
    Group("bastard", "default");
    Group("unarmed", "");
    Group("club", "nw_wblcl001");
    Group("scythe", "nw_wplsc001");
    Group("heavyflail", "nw_wblfh001");
    // Strength 29 (+9): one-handed, two-handed, and two daggers (main 10-13, off hand 5-8).
    Group("str.longsword", "nw_wswls001", "", 12);
    Group("str.greataxe", "nw_waxgr001", "", 12);
    Group("str.dual", "nw_wswdg001", "nw_wswdg001", 12);
    Window("str.dual", 1, 8);
    Group("str.quarterstaff", "nw_wdbqs001", "", 12);
    Window("str.quarterstaff", 1, 9);
    Group("str.rapier", "nw_wswrp001", "", 12);
    // Strength 6 (-2): a dagger can still deal 1.
    Group("weak.dagger", "nw_wswdg001", "", -11);
    Group("weak.greataxe", "nw_waxgr001", "", -11);
    // Damage effects: +1d6 fire, +5 to the weapon's own damage, -2.
    object a = Pair("fire", "nw_wswdg001");
    Perm(a, EffectDamageIncrease(DAMAGE_BONUS_1d6, DAMAGE_TYPE_FIRE));
    a = Pair("fire", "nw_wswdg001");
    Perm(a, EffectDamageIncrease(DAMAGE_BONUS_1d6, DAMAGE_TYPE_FIRE));
    a = Pair("plus5", "nw_wswdg001");
    Perm(a, EffectDamageIncrease(DAMAGE_BONUS_5, DAMAGE_TYPE_PIERCING));
    a = Pair("plus5", "nw_wswdg001");
    Perm(a, EffectDamageIncrease(DAMAGE_BONUS_5, DAMAGE_TYPE_PIERCING));
    a = Pair("minus2", "nw_wswls001");
    Perm(a, EffectDamageDecrease(2, DAMAGE_TYPE_SLASHING));
    a = Pair("minus2", "nw_wswls001");
    Perm(a, EffectDamageDecrease(2, DAMAGE_TYPE_SLASHING));
    // Weapon properties: +2 enhancement, +1d6 fire, -2 damage.
    int i;
    for (i = 0; i < 2; i++)
    {
        a = Pair("ip.enh2", "nw_wswls001");
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(2), GetItemPossessedBy(a, "NW_WSWLS001"));
        a = Pair("ip.fire", "nw_wswls001");
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6), GetItemPossessedBy(a, "NW_WSWLS001"));
        a = Pair("ip.penalty", "nw_wswls001");
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyDamagePenalty(2), GetItemPossessedBy(a, "NW_WSWLS001"));
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "dagger longsword greataxe bastard unarmed club scythe heavyflail str.longsword str.greataxe str.dual "
        + "str.quarterstaff str.rapier weak.dagger weak.greataxe plus5 minus2 ip.enh2 ip.penalty ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        PReportDamage(GetStringLeft(s, sp));
        s = GetSubString(s, sp + 1, 1000);
    }
    s = "greatsword fire ip.fire ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        int n = PGet(g + ".hit");
        PLogMean(g + ".mean", IntToFloat(PGet(g + ".dmg")), n, PSd(PGet(g + ".dmg"), PGet(g + ".dsq"), n), 0.05);
        s = GetSubString(s, sp + 1, 1000);
    }
    // The off-hand dagger and the quarterstaff's other end, picked out by their damage range.
    PLogI("str.dual.offhand.min", PGet("str.dual.winmin"));
    PLogI("str.dual.offhand.max", PGet("str.dual.winmax"));
    PLogRate("str.dual.offhand.share", PGet("str.dual.win"), PGet("str.dual.hit"));
    PLogI("str.quarterstaff.offhand.min", PGet("str.quarterstaff.winmin"));
    PLogI("str.quarterstaff.offhand.max", PGet("str.quarterstaff.winmax"));
    PLogRate("str.quarterstaff.offhand.share", PGet("str.quarterstaff.win"), PGet("str.quarterstaff.hit"));
    // Fire damage is reported apart from the weapon's.
    s = "fire ip.fire ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        int n = PGet(g + ".firen");
        PLogRate(g + ".reported", n, PGet(g + ".hit"));
        PLogI(g + ".part.min", PGet(g + ".firemin"));
        PLogI(g + ".part.max", PGet(g + ".firemax"));
        PLogMean(g + ".part.mean", IntToFloat(PGet(g + ".fire")), n, PSd(PGet(g + ".fire"), PGet(g + ".firesq"), n), 0.05);
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
