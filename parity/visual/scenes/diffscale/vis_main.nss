#include "visual_inc"
// diffscale: how the game difficulty scales weapon dice (run once per difficulty: visual.py --difficulty N).
// The player (elf wizard 1) takes a fighter level for the weapons. Every attacker has Strength 14 (+2).
// Phase A (crit-immune targets, 110 s, all at once):
//   the player hits dummy D1 (longsword 1d8 for 55 s, then greatsword 2d6) - player against NPC;
//   her henchman (a bandit, longsword 1d8) hits dummy D2 - associate against NPC;
//   two bandits (dagger 1d4) hit the henchman - NPC against associate;
//   two "Dagger" bandits (1d4) and two "Sword" bandits (longsword 1d8) hit the player - NPC against player.
// Phase B (55 s): the player hits dummy D3, not immune to criticals, with a keen longsword and Improved Critical
//   (1d8, x2) - is each critical roll scaled on its own?
// Phase D: her bonuses removed, 9999 damage: does she stop at 1 hit point?
// NPC targets log every hit (ds_dmg: "ds|dmg|<target>|<attacker>|<damage>"); hits on the player are in chat.txt.

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|ds|" + s); }
void Mark(string s) { SendMessageToPC(GetFirstPC(), "DS " + s); }

object Spawn(string sResRef, float fDx, float fDy, int nFaction, string sTag, string sName)
{
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(v.x + fDx, v.y + fDy, v.z), 0.0), FALSE, sTag);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetName(o, sName);
    return o;
}

void Tough(object o, int bCritImmune)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(9000), o);
    if (bCritImmune) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT), o);
    SetImmortal(o, TRUE);
}

object Dummy(float fDx, float fDy, string sTag, int bCritImmune)
{
    object d = Spawn("nw_goblina", fDx, fDy, STANDARD_FACTION_HOSTILE, sTag, sTag);
    SetEventScript(d, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "ds_dmg");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACDecrease(20), d);
    Tough(d, bCritImmune);
    return d;
}

void Arm(object o, string sResRef, int bKeen = FALSE)
{
    object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o);
    if (GetIsObjectValid(w)) DestroyObject(w);
    w = GetItemInSlot(INVENTORY_SLOT_LEFTHAND, o);
    if (GetIsObjectValid(w)) DestroyObject(w);
    w = CreateItemOnObject(sResRef, o);
    SetIdentified(w, TRUE);
    SetDroppableFlag(w, FALSE);
    if (bKeen) AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), w);
    AssignCommand(o, ActionEquipItem(w, INVENTORY_SLOT_RIGHTHAND));
}

// Strength 14 (+2) whatever the blueprint has.
void Str14(object o)
{
    int d = 14 - GetAbilityScore(o, ABILITY_STRENGTH);
    if (d > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, d), o);
    if (d < 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_STRENGTH, -d), o);
}

object Attacker(float fDx, float fDy, string sTag, string sName, string sWeapon, object oTarget)
{
    object a = Spawn("nw_bandit001", fDx, fDy, STANDARD_FACTION_HOSTILE, sTag, sName);
    Str14(a);
    SetPlotFlag(a, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), a);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectModifyAttacks(5), a);
    Arm(a, sWeapon);
    DelayCommand(1.0, AssignCommand(a, ActionAttack(oTarget)));
    return a;
}

void Stats(object o)
{
    object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o);
    Note("stats|" + GetTag(o) + "|" + GetName(o) + "|str=" + IntToString(GetAbilityScore(o, ABILITY_STRENGTH)) + "|strmod=" + IntToString(GetAbilityModifier(ABILITY_STRENGTH, o))
        + "|weapon=" + GetTag(w) + "|base=" + IntToString(GetBaseItemType(w)) + "|monk=" + IntToString(GetLevelByClass(CLASS_TYPE_MONK, o)) + "|size=" + IntToString(GetCreatureSize(o)));
}

void PcAttack(object pc, string sWeapon, string sTarget, int bKeen)
{
    AssignCommand(pc, ClearAllActions(TRUE));
    Mark("pc." + sWeapon);
    Note("phase|pc." + sWeapon);
    Arm(pc, sWeapon, bKeen);
    DelayCommand(2.5, Stats(pc));
    DelayCommand(1.0, AssignCommand(pc, ActionAttack(GetObjectByTag(sTarget))));
}

void Stop(string sTag)
{
    int i = 0;
    object o = GetObjectByTag(sTag, i);
    while (GetIsObjectValid(o))
    {
        AssignCommand(o, ClearAllActions(TRUE));
        DestroyObject(o);
        o = GetObjectByTag(sTag, ++i);
    }
}

void PhaseA(object pc)
{
    Mark("phaseA");
    Dummy(1.5, 0.0, "D1", TRUE);
    object oDum2 = Dummy(-6.0, 6.0, "D2", TRUE);
    object h = Spawn("nw_bandit001", -7.5, 6.0, STANDARD_FACTION_COMMONER, "H", "Hench");
    Str14(h);
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "ds_dmg");
    Tough(h, TRUE);
    AddHenchman(pc, h);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), h);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectModifyAttacks(5), h);
    Arm(h, "nw_wswls001");
    DelayCommand(1.0, AssignCommand(h, ActionAttack(oDum2)));
    Attacker(-9.0, 6.0, "GH", "Hbiter", "nw_wswdg001", h);
    Attacker(-7.5, 7.5, "GH", "Hbiter", "nw_wswdg001", h);
    Attacker(0.0, 1.6, "GP", "Dagger", "nw_wswdg001", pc);
    Attacker(0.0, -1.6, "GP", "Dagger", "nw_wswdg001", pc);
    Attacker(-1.5, 0.5, "GP", "Sword", "nw_wswls001", pc);
    Attacker(-1.2, -1.0, "GP", "Sword", "nw_wswls001", pc);
    PcAttack(pc, "nw_wswls001", "D1", FALSE);
    DelayCommand(3.0, Stats(h));
    DelayCommand(3.0, Stats(GetObjectByTag("GH")));
    DelayCommand(3.0, Stats(GetObjectByTag("GP", 0)));
    DelayCommand(3.0, Stats(GetObjectByTag("GP", 2)));
}

void PhaseB(object pc)
{
    Stop("GP"); Stop("GH"); Stop("D1"); Stop("D2");
    object h = GetObjectByTag("H");
    AssignCommand(h, ClearAllActions(TRUE));
    Mark("phaseB");
    Note("phase|B");
    Dummy(1.5, 0.0, "D3", FALSE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_IMPROVED_CRITICAL_LONG_SWORD), pc);
    PcAttack(pc, "nw_wswls001", "D3", TRUE);
}

void PhaseD(object pc)
{
    AssignCommand(pc, ClearAllActions(TRUE));
    Stop("D3");
    object f = GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc);

    Mark("phaseD");
    effect e = GetFirstEffect(pc);
    while (GetIsEffectValid(e)) { RemoveEffect(pc, e); e = GetNextEffect(pc); }
    SetImmortal(pc, FALSE);
    SetPlotFlag(pc, FALSE);
}

void PhaseD2(object pc)
{
    Note("minhp|before=" + IntToString(GetCurrentHitPoints(pc)));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(9999), pc);
    DelayCommand(0.5, Note("minhp|after=" + IntToString(GetCurrentHitPoints(pc)) + "|dead=" + IntToString(GetIsDead(pc))));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    if (GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(1.0, VCamera(pc, 270.0, 18.0, 40.0));
    SetPlotFlag(pc, FALSE);
    SetMaxHenchmen(4);
    Note("difficulty|" + IntToString(GetGameDifficulty()));
    Tough(pc, TRUE);
    SetXP(pc, 1000);
    LevelUpHenchman(pc, CLASS_TYPE_FIGHTER, TRUE);
    Note("pc|hd=" + IntToString(GetHitDice(pc)) + "|fighter=" + IntToString(GetLevelByClass(CLASS_TYPE_FIGHTER, pc)));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectModifyAttacks(5), pc);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectHaste(), pc);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), pc);
    DelayCommand(2.0, PhaseA(pc));
    DelayCommand(57.0, PcAttack(pc, "nw_wswgs001", "D1", FALSE));
    DelayCommand(112.0, PhaseB(pc));
    DelayCommand(175.0, PhaseD(pc));
    DelayCommand(176.0, PhaseD2(pc));
    DelayCommand(178.0, VDone());
}
