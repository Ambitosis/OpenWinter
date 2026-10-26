// NWNX_Damage: the attack event (every attack as it is swung, OBJECT_SELF the attacker) and the damage event (damage
// about to be dealt, OBJECT_SELF the damaged object), their data and what changing it does; per-object scripts;
// DealDamage. nw_drowfight010 attackers with +20 attack fight passive, immortal nw_bandit001 targets.
#include "pcombat_inc"
#include "nwnx_damage"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o); }

object Pair(string g, string sWeapon = "nw_wswls001", int nAttack = 20, string sAttacker = FIGHTER)
{
    object a = PPair(g, sAttacker, TARGET);
    if (nAttack > 0) Perm(a, EffectAttackIncrease(nAttack));
    if (nAttack < 0) Perm(a, EffectAttackDecrease(-nAttack));
    if (sAttacker == FIGHTER)
    {
        PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
        if (sWeapon != "") SetLocalObject(a, "W", PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND));
        if (sWeapon == "nw_wbwln001") PEquip(a, "nw_wamar001", INVENTORY_SLOT_ARROWS, 99);
    }
    return a;
}

object Target(string sTag, float x, float y)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object t = CreateObject(OBJECT_TYPE_CREATURE, TARGET, Location(oArea, Vector(x, y, 0.0), 0.0), FALSE, sTag);
    PStrip(t);
    SetLocalString(t, "G", sTag);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "nd_od");
    SetImmortal(t, TRUE);
    return t;
}

void Scripted2()
{
    struct NWNX_Damage_DamageData de;
    de.iFire = 10;
    NWNX_Damage_DealDamage(de, GetObjectByTag("S2"), GetObjectByTag("S4"));
}

void Scripted()
{
    object t1 = GetObjectByTag("S1"); object t2 = GetObjectByTag("S2"); object t3 = GetObjectByTag("S3");
    object t4 = GetObjectByTag("S4"); object t5 = GetObjectByTag("S5"); object t6 = GetObjectByTag("S6");
    object t7 = GetObjectByTag("S7"); object p = GetObjectByTag("SP");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(5, DAMAGE_TYPE_SLASHING), t1);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(10, DAMAGE_TYPE_FIRE), t2);
    struct NWNX_Damage_DamageData dd;
    dd.iPierce = 3; dd.iSlash = 4; dd.iMagical = 2; dd.iCustom1 = 2;
    NWNX_Damage_DealDamage(dd, t3, t4);
    PLogI("S3.after", GetCurrentHitPoints(t3));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(10, DAMAGE_TYPE_FIRE), t5);
    struct NWNX_Damage_DamageData ds;
    ds.iSlash = 10;
    NWNX_Damage_DealDamage(ds, t6, t4);
    ds.iPower = 2;
    NWNX_Damage_DealDamage(ds, t7, t4);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(3, DAMAGE_TYPE_ACID), p);
    PLogI("SP.hp", GetCurrentHitPoints(p));
    AssignCommand(GetObjectByTag("S9"), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, GetObjectByTag("S8"), METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
}

int Is(int b) { return b ? 1 : 0; }

void Report()
{
    string s = "plain miss miss2hit hit2miss rogue bow twf unarmed haste dog kill own atkmod dmgmod fireimm nat20 ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        s = GetSubString(s, sp + 1, 1000);
        int ev = PGet(g + ".ev");
        int hits = PGet(g + ".res1") + PGet(g + ".res3") + PGet(g + ".res7") + PGet(g + ".res10");
        PLogI(g + ".any", Is(ev > 0));
        PLogI(g + ".everyattack", Is(ev == PGet(g + ".atk")));
        PLogI(g + ".everyhit", Is(PGet(g + ".dev") == PGet(g + ".hit")));
        int i, watset = 0, resset = 0;
        for (i = 1; i <= 8; i++) if (PGet(g + ".wat" + IntToString(i)) > 0) watset += 1 << i;
        PLogI(g + ".watset", watset);
        PLogI(g + ".sneak", Is(PGet(g + ".sneak0") == ev) + 2 * Is(PGet(g + ".sneak1") == ev));
        PLogI(g + ".ranged", Is(PGet(g + ".rng0") == ev) + 2 * Is(PGet(g + ".rng1") == ev));
        PLogI(g + ".type0", Is(PGet(g + ".type0") == ev));
        PLogI(g + ".consistent", PGet(g + ".hitnobase") + PGet(g + ".missbase") + PGet(g + ".basediff") + PGet(g + ".firediff")
            + PGet(g + ".odbad") + PGet(g + ".spell"));
        for (i = 1; i <= 3; i++)
            if (GetLocalInt(GetModule(), g + ".mod" + IntToString(i) + ".set")) PLogI(g + ".mod" + IntToString(i), PGet(g + ".mod" + IntToString(i)));
    }
    PLogI("miss.allmiss", Is(PGet("miss.res4") + PGet("miss.res1") == PGet("miss.ev") && PGet("miss.res4") > 0));
    PLogI("miss2hit.allhit", Is(PGet("miss2hit.hit") == PGet("miss2hit.ev") && PGet("miss2hit.res4") > 0));
    PLogI("hit2miss.none", Is(PGet("hit2miss.hit") == 0 && PGet("hit2miss.dev") == 0 && PGet("hit2miss.ev") > 0));
    PLogI("twf.offnum3", Is(PGet("twf.wat2") > 0 && PGet("twf.wat2.n3") == PGet("twf.wat2")));
    PLogI("haste.extranum3", Is(PGet("haste.wat6") > 0 && PGet("haste.wat6.n3") == PGet("haste.wat6")));
    PLogI("kill.blows", PGet("kill.kill1"));
    PLogI("own.attack", Is(PGet("own.own") == PGet("own.atk") && PGet("own.ev") == 0 && PGet("own.atk") > 0));
    PLogI("own.damage", Is(PGet("own.downs") == PGet("own.hit") && PGet("own.dev") == 0 && PGet("own.hit") > 0));
    int fireHits = PGet("fireimm.res1") + PGet("fireimm.res3") + PGet("fireimm.res7");
    PLogI("fireimm.fire0", Is(fireHits > 0 && PGet("fireimm.hitfire0") == fireHits));
    int r20 = 0, j;
    for (j = 1; j <= 10; j++) if (PGet("nat20.r20.res" + IntToString(j)) > 0) r20 += 1 << j;
    // An ordinary hit (a critical one when the confirmation roll is a 20 too).
    PLogI("nat20.results", Is((r20 & 2) != 0 && (r20 & ~10) == 0));
    PDone();
}

void main()
{
    int i;
    PClockStart(40.0);
    NWNX_Damage_SetAttackEventScript("nd_atk");
    NWNX_Damage_SetDamageEventScript("nd_dmg");
    Pair("plain");
    Pair("miss", "nw_wswls001", -20);
    Pair("miss2hit", "nw_wswls001", -20);
    Pair("hit2miss");
    object a = Pair("rogue", "nw_wswdg001");
    Perm(a, EffectBonusFeat(353));
    Pair("bow", "nw_wbwln001");
    a = Pair("twf");
    PEquip(a, "nw_wswdg001", INVENTORY_SLOT_LEFTHAND);
    Pair("unarmed", "");
    a = Pair("haste");
    Perm(a, EffectHaste());
    Pair("dog", "", 20, "nw_dog");
    a = Pair("kill");
    SetImmortal(GetLocalObject(a, "T"), FALSE);
    a = Pair("own");
    NWNX_Damage_SetAttackEventScript("nd_atk2", a);
    NWNX_Damage_SetDamageEventScript("nd_dmg2", GetLocalObject(a, "T"));
    Pair("atkmod");
    Pair("dmgmod");
    a = Pair("fireimm");
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6), GetLocalObject(a, "W"));
    Perm(GetLocalObject(a, "T"), EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE, 100));
    // Natural 20s against an AC the attack bonus can't reach.
    for (i = 0; i < 3; i++)
    {
        a = Pair("nat20", "nw_wswls001", -20);
        Perm(a, EffectModifyAttacks(5));
        Perm(GetLocalObject(a, "T"), EffectACIncrease(20, AC_DEFLECTION_BONUS));
    }

    Target("S1", 5.0, 75.0);
    object t2 = Target("S2", 15.0, 75.0);
    Perm(t2, EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE, 50));
    Target("S3", 25.0, 75.0);
    object t5 = Target("S5", 45.0, 75.0);
    Perm(t5, EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE, 50));
    object t6 = Target("S6", 55.0, 75.0);
    Perm(t6, EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE));
    object t7 = Target("S7", 65.0, 75.0);
    Perm(t7, EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE));
    object oArea = GetObjectByTag("PARITY_AREA");
    object c = CreateObject(OBJECT_TYPE_CREATURE, TARGET, Location(oArea, Vector(35.0, 75.0, 0.0), 0.0), FALSE, "S4");
    PStrip(c);
    SetImmortal(c, TRUE);
    object t8 = Target("S8", 35.0, 70.0);
    SetEventScript(t8, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "");
    object m = CreateObject(OBJECT_TYPE_CREATURE, "nw_drowmage020", Location(oArea, Vector(33.0, 70.0, 0.0), 0.0), FALSE, "S9");
    PStrip(m);
    SetImmortal(m, TRUE);
    ChangeToStandardFaction(m, STANDARD_FACTION_DEFENDER);
    ChangeToStandardFaction(t8, STANDARD_FACTION_HOSTILE);
    PEnemies(m, t8);
    object p = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", Location(oArea, Vector(75.0, 75.0, 0.0), 0.0), FALSE, "SP");
    SetLocalString(p, "G", "SP");
    SetPlotFlag(p, FALSE);
    PStartAll(3.0);
    DelayCommand(25.0, Scripted());
    DelayCommand(27.0, Scripted2());
    DelayCommand(34.0, Report());
}
