// Haste and Slow on armour class, Defensive Roll and Death Attack paralysis.
// 1. GetAC of a bandit plain, hasted, slowed, both, and with a Haste item property; and the hit rate of an attacker
//    (+8 to hit) on each.
// 2. Defensive Roll (bonus feat, Reflex +30): a hit that would kill (a weapon hit for 12-21, or 20
//    points of scripted damage) on 11 hit points, on a passive target and on one fighting someone else; a second blow.
// 3. Death Attack 1 (bonus feat, with Sneak Attack) on a flat-footed target with Fortitude -30: is it paralysed, for
//    how long?
#include "pcombat_inc"

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Target(string g, int nKind)
{
    object a = PPair(g, "nw_drowfight010", "nw_bandit001");
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(3));
    Perm(a, EffectAttackIncrease(2));
    object t = GetLocalObject(a, "T");
    Perm(t, EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
    if (nKind == 1) Perm(t, EffectHaste());
    if (nKind == 2) Perm(t, EffectSlow());
    if (nKind == 3) { Perm(t, EffectHaste()); Perm(t, EffectSlow()); }
    if (nKind == 4)
    {
        object r = CreateItemOnObject("nw_it_mring021", t);
        if (!GetIsObjectValid(r)) r = CreateItemOnObject("nw_maarcl001", t);
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyHaste(), r);
        AssignCommand(t, ActionEquipItem(r, INVENTORY_SLOT_LEFTRING));
    }
    SetLocalObject(GetModule(), g + ".t", t);
    return a;
}

void LogAC(string g)
{
    PLogI(g + ".ac", GetAC(GetLocalObject(GetModule(), g + ".t")));
}

// A Defensive Roll target that a blow would kill; the attacker strikes once (bWeapon) or 30 scripted damage lands.
void Roller(string g, int bWeapon, int bBusy, int nBlows)
{
    object a = PPair(g, "nw_drowfight010", "nw_bandit001");
    object t = GetLocalObject(a, "T");
    SetImmortal(t, FALSE);
    SetLocalObject(GetModule(), g + "." + IntToString(PGet(g + ".n")), t);
    PAdd(g + ".n");
    Perm(t, EffectBonusFeat(FEAT_DEFENSIVE_ROLL));
    Perm(t, EffectSavingThrowIncrease(SAVING_THROW_REFLEX, 30));
    Perm(t, EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(GetCurrentHitPoints(t) - 11), t);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_ex_dmg");
    if (bBusy)
    {
        // It fights a dummy beside it, so it isn't flat-footed.
        vector v = GetPosition(t);
        object d = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(t), Vector(v.x, v.y + 1.4, 0.0), 0.0));
        PStrip(d);
        SetImmortal(d, TRUE);
        ChangeToStandardFaction(d, STANDARD_FACTION_HOSTILE);
        AssignCommand(t, ActionAttack(d));
    }
    if (bWeapon)
    {
        SetBaseAttackBonus(1, a);
        Perm(a, EffectAttackIncrease(30));
        Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
        SetLocalInt(a, "BLOWS", nBlows);
    }
    else
    {
        SetLocalObject(a, "T", OBJECT_INVALID);
        DelayCommand(3.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(20), t));
        if (nBlows > 1) DelayCommand(6.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(20), t));
    }
}

void LogRollers(string g)
{
    int i, alive = 0, hp = 0;
    for (i = 0; i < PGet(g + ".n"); i++)
    {
        object t = GetLocalObject(GetModule(), g + "." + IntToString(i));
        if (GetIsObjectValid(t) && !GetIsDead(t)) { alive++; hp += GetCurrentHitPoints(t); }
    }
    // Most of a group lives, or none does (a roller can be caught flat-footed before it starts fighting).
    PLogI(g + ".mostly.alive", alive * 2 > PGet(g + ".n"));
    PLogI(g + ".n", PGet(g + ".n"));
}

object Assassin(string g)
{
    object a = PPair(g, "nw_drowfight010", "nw_bandit001");
    SetBaseAttackBonus(1, a);
    Perm(a, EffectAttackIncrease(30));
    Perm(a, EffectBonusFeat(FEAT_PRESTIGE_DEATH_ATTACK_1));
    Perm(a, EffectBonusFeat(FEAT_SNEAK_ATTACK));
    object t = GetLocalObject(a, "T");
    Perm(t, EffectSavingThrowDecrease(SAVING_THROW_FORT, 30));
    Perm(t, EffectTemporaryHitpoints(500));
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_ex_dmg");
    SetLocalObject(GetModule(), g + "." + IntToString(PGet(g + ".n")), t);
    PAdd(g + ".n");
    return a;
}

void Paralysed(string g, string sAt)
{
    int i, n = 0;
    for (i = 0; i < PGet(g + ".n"); i++)
    {
        object t = GetLocalObject(GetModule(), g + "." + IntToString(i));
        effect e = GetFirstEffect(t);
        while (GetIsEffectValid(e))
        {
            if (GetEffectType(e) == EFFECT_TYPE_PARALYZE) { n++; break; }
            e = GetNextEffect(t);
        }
    }
    PLogI(g + ".paralysed." + sAt, n);
}

void Setup()
{
    int i;
    for (i = 0; i < 3; i++)
    {
        Target("plain", 0);
        Target("haste", 1);
        Target("slow", 2);
        Target("both", 3);
        Target("ipHaste", 4);
        Roller("roll.weapon", TRUE, FALSE, 1);
        Roller("roll.weapon.busy", TRUE, TRUE, 1);
        Roller("roll.script", FALSE, FALSE, 1);
        Roller("roll.script.twice", FALSE, FALSE, 2);
        Assassin("death");
    }
    DelayCommand(2.0, LogAC("plain"));
    DelayCommand(2.0, LogAC("haste"));
    DelayCommand(2.0, LogAC("slow"));
    DelayCommand(2.0, LogAC("both"));
    DelayCommand(2.0, LogAC("ipHaste"));
    // Armour class lowered below zero.
    object low = PSpawn("nw_bandit001", 70.0, 70.0);
    Perm(low, EffectACDecrease(20));
    SetLocalObject(GetModule(), "low20.t", low);
    DelayCommand(2.0, LogAC("low20"));
    PStartAll(1.0);
    float f;
    for (f = 2.0; f <= 30.0; f += 2.0) DelayCommand(f, Paralysed("death", IntToString(FloatToInt(f))));
}

void Report()
{
    string s = "plain haste slow both ipHaste ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PReportHits(g);
        s = GetSubString(s, sp + 1, 1000);
    }
    LogRollers("roll.weapon");
    LogRollers("roll.weapon.busy");
    LogRollers("roll.script");
    LogRollers("roll.script.twice");
    PLogI("death.damaged", PGet("death.hit"));
    PDone();
}

void main()
{
    PClockStart(45.0);
    DelayCommand(1.0, Setup());
    DelayCommand(34.0, Report());
}
