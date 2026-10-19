// When a hit's damage lands, measured from the attack: OnPhysicalAttacked fires with the swing; OnDamaged and the
// target's lost hit points come with the damage. Melee (one and two attacks a round, in separate swings) and ranged attackers on
// passive targets that almost never dodge (bows at 3, 8 and 16 m); a killing blow's OnDeath. Times in twentieths of a second (PClock).
#include "pcombat_inc"

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Pair(string g, int nAttacks, string sWeapon = "", string sAmmo = "", float fDist = 1.5)
{
    object a = PPair(g, "nw_drowfight010", "nw_bandit001", fDist);
    SetBaseAttackBonus(nAttacks, a);
    Perm(a, EffectAttackIncrease(30));
    object t = GetLocalObject(a, "T");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_hd_atk");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_hd_dmg");
    Perm(t, EffectTemporaryHitpoints(2000));
    if (sWeapon != "")
    {
        PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
        PUnequip(a, INVENTORY_SLOT_LEFTHAND);
        PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND);
        if (sAmmo != "") PEquip(a, sAmmo, INVENTORY_SLOT_ARROWS, 99);
    }
    return a;
}

// Polls the target's hit points every tick: the first tick after each attack where they dropped.
void Poll(object t)
{
    int hp = GetCurrentHitPoints(t);
    int last = GetLocalInt(t, "HPLAST");
    if (last > 0 && hp < last && GetLocalInt(t, "ATKAT") > 0 && GetLocalInt(t, "POLLED") == 0)
    {
        string g = GetLocalString(t, "G");
        PAdd(g + ".hpdrop", PClock() - GetLocalInt(t, "ATKAT"));
        PAdd(g + ".hpdropn");
        SetLocalInt(t, "POLLED", 1);
    }
    SetLocalInt(t, "HPLAST", hp);
    if (PClock() < 900) DelayCommand(0.05, Poll(t));
}

void Killer(string g)
{
    object a = PPair(g, "nw_drowfight010", "nw_bandit001");
    SetBaseAttackBonus(1, a);
    Perm(a, EffectAttackIncrease(30));
    Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
    object t = GetLocalObject(a, "T");
    SetImmortal(t, FALSE);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_hd_atk");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_hd_dmg");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DEATH, "pc_hd_die");
}

// A creature killed by scripted damage: does its OnDamaged run?
void ScriptedKill(int n)
{
    object t = PSpawn("nw_bandit001", 5.0 + 3.0 * IntToFloat(n), 75.0);
    SetLocalString(t, "G", "skill");
    SetLocalInt(t, "ATKAT", 1);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_hd_dmg");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DEATH, "pc_hd_die");
    DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(100), t));
}

void Setup()
{
    int i;
    for (i = 0; i < 5; i++)
    {
        Pair("melee1", 1);
        Pair("melee2", 2);
        Pair("bow3", 1, "nw_wbwsh001", "nw_wamar001", 3.0);
        Pair("bow8", 1, "nw_wbwsh001", "nw_wamar001", 8.0);
        Pair("bow16", 1, "nw_wbwsh001", "nw_wamar001", 16.0);
        Killer("kill");
        ScriptedKill(i);
    }
    object o = GetFirstObjectInArea(GetObjectByTag("PARITY_AREA"));
    while (GetIsObjectValid(o))
    {
        if (GetIsObjectValid(GetLocalObject(o, "A")) && GetLocalString(o, "G") != "kill") Poll(o);
        o = GetNextObjectInArea(GetObjectByTag("PARITY_AREA"));
    }
    PStartAll(1.0);
}

void Mean(string sKey, string sSum, string sN, float fTol)
{
    int n = PGet(sN);
    if (n <= 0) { PLog(sKey, "none"); return; }
    PLogF(sKey, IntToFloat(PGet(sSum)) / IntToFloat(n) / 20.0, fTol);
}

void Report()
{
    string s = "melee1 melee2 bow3 bow8 bow16 kill ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        if (g != "kill")
        {
            Mean(g + ".damaged.after.attack", g + ".lag", g + ".lagn", 0.45);
            Mean(g + ".hp.after.attack", g + ".hpdrop", g + ".hpdropn", 0.2);
        }
        s = GetSubString(s, sp + 1, 1000);
    }
    Mean("kill.death.after.attack", "kill.die", "kill.dien", 0.2);
    // A killing blow, by weapon or script, runs OnDeath but not OnDamaged.
    PLogI("kill.deaths", PGet("kill.dien"));
    PLogI("kill.damaged.events", PGet("kill.lagn"));
    PLogI("skill.deaths", PGet("skill.dien"));
    PLogI("skill.damaged.events", PGet("skill.lagn"));
    PDone();
}

void main()
{
    PClockStart(50.0);
    DelayCommand(1.0, Setup());
    DelayCommand(40.0, Report());
}
