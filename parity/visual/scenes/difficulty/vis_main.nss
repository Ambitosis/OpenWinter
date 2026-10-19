#include "visual_inc"
// difficulty: what the game difficulty does to a player (run once per difficulty: visual.py --difficulty N). The chat
// window (chat.txt) shows every attack and damage line; notes give the hit points each scripted hit took.
// 1. Scripted damage (EffectDamage) to the player from a hostile creature (20, 7, 3 and 1 points), the module, the
//    player herself, a commoner and a placeable; through 5 fire resistance and 25% fire immunity; from a hostile
//    creature to her henchman; and from her to a hostile creature.
// 2. Weapon hits: three drow fighters (+20 to hit, Str +12, keen longsword with Improved Critical: threat 12-20)
//    attack her for 24 s.
// 3. Attacks of opportunity: a bandit fights her while she shoots a light crossbow at it, drinks two potions, casts
//    Magic Missile and walks away.
// 4. A bandit and an immortal goblin with their stock scripts fight each other 7 m away (scene mp_delivery's case).

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Mark(string s) { SendMessageToPC(GetFirstPC(), "DIFF " + s); }

// A creature at an offset from the player, without its event scripts, in a standard faction.
object Spawn(string sResRef, float fDx, float fDy, int nFaction, string sTag)
{
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(v.x + fDx, v.y + fDy, v.z), 0.0), FALSE, sTag);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Hurt(object oTarget, int nDamage, int nType)
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(nDamage, nType), oTarget);
}

void Before(object o) { SetLocalInt(o, "HP0", GetCurrentHitPoints(o)); }
void After(object o, string sCase) { Note("scripted." + sCase + "." + IntToString(GetLocalInt(o, "HP0") - GetCurrentHitPoints(o))); }

// oSource deals nDamage of nType to oTarget at fAt; the hit points it lost are noted.
void Scripted(float fAt, string sCase, object oSource, object oTarget, int nDamage, int nType)
{
    DelayCommand(fAt - 0.3, Before(oTarget));
    DelayCommand(fAt - 0.2, Mark(sCase));
    DelayCommand(fAt, AssignCommand(oSource, Hurt(oTarget, nDamage, nType)));
    DelayCommand(fAt + 0.5, After(oTarget, sCase));
}

void Melee(object pc)
{
    Mark("melee");
    Note("melee.start.hp." + IntToString(GetCurrentHitPoints(pc)));
    int i;
    for (i = 0; i < 3; i++)
    {
        float ang = 120.0 * IntToFloat(i);
        object a = Spawn("nw_drowfight010", 1.6 * cos(ang), 1.6 * sin(ang), STANDARD_FACTION_HOSTILE, "diff_melee");
        SetPlotFlag(a, TRUE);
        SetBaseAttackBonus(1, a);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), a);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 12), a);
        object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, a);
        if (GetIsObjectValid(w)) DestroyObject(w);
        w = GetItemInSlot(INVENTORY_SLOT_LEFTHAND, a);
        if (GetIsObjectValid(w)) DestroyObject(w);
        w = CreateItemOnObject("nw_wswls001", a);
        SetIdentified(w, TRUE);
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), w);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_IMPROVED_CRITICAL_LONG_SWORD), a);
        AssignCommand(a, ActionEquipItem(w, INVENTORY_SLOT_RIGHTHAND));
        AssignCommand(a, ActionAttack(pc));
    }
}

void EndMelee(object pc)
{
    int i;
    for (i = 0; i < 3; i++)
    {
        object o = GetObjectByTag("diff_melee", i);
        AssignCommand(o, ClearAllActions(TRUE));
        DestroyObject(o);
    }
    Note("melee.end.hp." + IntToString(GetCurrentHitPoints(pc)));
    Mark("melee.end");
}

void Guard(object pc)
{
    object g = Spawn("nw_bandit001", 1.4, 0.0, STANDARD_FACTION_HOSTILE, "diff_guard");
    SetPlotFlag(g, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(10), g);
    AssignCommand(g, ActionAttack(pc));
    object x = CreateItemOnObject("nw_wbwxl001", pc);
    object b = CreateItemOnObject("nw_wambo001", pc, 99);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, ActionEquipItem(x, INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(pc, ActionEquipItem(b, INVENTORY_SLOT_BOLTS));
}

void Shoot(object pc) { Mark("aoo.shoot"); AssignCommand(pc, ActionAttack(GetObjectByTag("diff_guard"))); }

void StopShooting(object pc)
{
    Mark("aoo.shoot.end");
    AssignCommand(pc, ClearAllActions(TRUE));
    object x = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, pc);
    if (GetIsObjectValid(x)) AssignCommand(pc, ActionUnequipItem(x));
}

void Drink(object pc, string sCase)
{
    Mark("aoo." + sCase);
    object p = CreateItemOnObject("nw_it_mpotion001", pc);
    AssignCommand(pc, ActionUseItemOnObject(p, GetFirstItemProperty(p), pc));
}

void Cast(object pc)
{
    Mark("aoo.cast");
    AssignCommand(pc, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, GetObjectByTag("diff_guard"), METAMAGIC_ANY, TRUE));
}

void Walk(object pc)
{
    Mark("aoo.walk");
    vector v = GetPosition(pc);
    AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(v.x - 6.0, v.y, v.z), 0.0)));
}

void EndGuard(object pc)
{
    object g = GetObjectByTag("diff_guard");
    AssignCommand(g, ClearAllActions(TRUE));
    DestroyObject(g);
    Mark("aoo.end");
}

// mp_delivery's fight: both keep their stock scripts, both immortal, attacking each other.
void Fight(object pc)
{
    Mark("fight");
    vector v = GetPosition(pc);
    object a = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(v.x - 4.0, v.y + 6.0, v.z), 0.0), FALSE, "diff_fa");
    object b = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(GetArea(pc), Vector(v.x - 2.5, v.y + 6.0, v.z), 180.0), FALSE, "diff_fb");
    ChangeToStandardFaction(a, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(b, STANDARD_FACTION_HOSTILE);
    SetImmortal(a, TRUE);
    SetImmortal(b, TRUE);
    SetAILevel(a, AI_LEVEL_NORMAL);
    SetAILevel(b, AI_LEVEL_NORMAL);
    DelayCommand(0.5, AssignCommand(a, ActionAttack(b)));
    DelayCommand(0.5, AssignCommand(b, ActionAttack(a)));
    DelayCommand(9.5, Note("fight.hp.bandit." + IntToString(GetCurrentHitPoints(a)) + ".goblin." + IntToString(GetCurrentHitPoints(b))));
    DelayCommand(10.0, DestroyObject(a));
    DelayCommand(10.0, DestroyObject(b));
    DelayCommand(10.1, Mark("fight.end"));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(1.0, VCamera(pc, 270.0, 18.0, 40.0));
    SetPlotFlag(pc, FALSE);
    Note("difficulty." + IntToString(GetGameDifficulty()));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(3000), pc);
    Note("start.hp." + IntToString(GetCurrentHitPoints(pc)));

    object hostile = Spawn("nw_goblina", 8.0, -6.0, STANDARD_FACTION_HOSTILE, "diff_src");
    SetPlotFlag(hostile, TRUE);
    object commoner = Spawn("nw_bandit001", 9.0, -6.0, STANDARD_FACTION_COMMONER, "diff_comm");
    SetPlotFlag(commoner, TRUE);
    vector v = GetPosition(pc);
    object plc = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", Location(GetArea(pc), Vector(v.x + 10.0, v.y - 6.0, v.z), 0.0));
    object hench = Spawn("nw_bandit001", -2.0, -3.0, STANDARD_FACTION_COMMONER, "diff_hench");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(500), hench);
    AddHenchman(pc, hench);
    object dummy = Spawn("nw_goblina", 7.0, -8.0, STANDARD_FACTION_HOSTILE, "diff_dummy");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(500), dummy);

    float t = 2.0;
    Scripted(t, "hostile.20", hostile, pc, 20, DAMAGE_TYPE_MAGICAL); t += 1.0;
    Scripted(t, "hostile.7", hostile, pc, 7, DAMAGE_TYPE_MAGICAL); t += 1.0;
    Scripted(t, "hostile.3", hostile, pc, 3, DAMAGE_TYPE_MAGICAL); t += 1.0;
    Scripted(t, "hostile.1", hostile, pc, 1, DAMAGE_TYPE_MAGICAL); t += 1.0;
    Scripted(t, "module.20", GetModule(), pc, 20, DAMAGE_TYPE_MAGICAL); t += 1.0;
    Scripted(t, "self.20", pc, pc, 20, DAMAGE_TYPE_MAGICAL); t += 1.0;
    Scripted(t, "commoner.20", commoner, pc, 20, DAMAGE_TYPE_MAGICAL); t += 1.0;
    Scripted(t, "placeable.20", plc, pc, 20, DAMAGE_TYPE_MAGICAL); t += 1.0;
    DelayCommand(t - 0.5, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDamageResistance(DAMAGE_TYPE_FIRE, 5), pc, 1.0));
    Scripted(t, "resist5.fire.20", hostile, pc, 20, DAMAGE_TYPE_FIRE); t += 1.5;
    DelayCommand(t - 0.5, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE, 25), pc, 1.0));
    Scripted(t, "immune25.fire.20", hostile, pc, 20, DAMAGE_TYPE_FIRE); t += 1.5;
    Scripted(t, "henchman.20", hostile, hench, 20, DAMAGE_TYPE_MAGICAL); t += 1.0;
    Scripted(t, "pc.on.hostile.20", pc, dummy, 20, DAMAGE_TYPE_MAGICAL); t += 1.0;
    DelayCommand(t, DestroyObject(dummy));

    DelayCommand(2.0, Fight(pc));

    DelayCommand(16.0, Melee(pc));
    DelayCommand(40.0, EndMelee(pc));

    DelayCommand(42.0, Guard(pc));
    DelayCommand(45.0, Shoot(pc));
    DelayCommand(52.0, StopShooting(pc));
    DelayCommand(53.0, Drink(pc, "potion1"));
    DelayCommand(57.0, Drink(pc, "potion2"));
    DelayCommand(61.0, Cast(pc));
    DelayCommand(66.0, Walk(pc));
    DelayCommand(71.0, EndGuard(pc));
    DelayCommand(72.0, Note("end.hp." + IntToString(GetCurrentHitPoints(pc))));
    DelayCommand(73.0, VDone());
}
