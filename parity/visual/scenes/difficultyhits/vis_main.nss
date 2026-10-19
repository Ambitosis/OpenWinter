#include "visual_inc"
// difficultyhits: how the game difficulty changes weapon damage and critical hits on a player (run once per
// difficulty: visual.py --difficulty N; the chat window has every attack and damage line).
// 1. Three drow fighters with daggers (1d4, Str +12) attack her for 11 s while she is immune to critical hits, so
//    every hit is 1d4 + the same bonus and the damage values show how difficulty scales it.
// 2. The same with daggers that also deal 2 fire damage (does the scaling reach a weapon's damage bonus?).
// 3. Keen longswords with Improved Critical (threat 12-20), not immune: critical hits or not.
// 4. A hostile goblin casts Magic Missile at her, and deals her 20, 7 and 1 points by script (EffectDamage).
// 5. A bandit and an immortal goblin with their stock scripts fight each other 7 m away (scene mp_delivery's case).

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Mark(string s) { SendMessageToPC(GetFirstPC(), "DIFF " + s); }

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

void Hurt(object oTarget, int nDamage)
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(nDamage, DAMAGE_TYPE_MAGICAL), oTarget);
}

// nKind: 0 dagger, 1 dagger with 2 fire damage, 2 keen longsword with Improved Critical.
void Group(object pc, int nKind, string sTag)
{
    Mark(sTag);
    int i;
    for (i = 0; i < 3; i++)
    {
        float ang = 120.0 * IntToFloat(i) + 40.0 * IntToFloat(nKind);
        object a = Spawn("nw_drowfight010", 1.6 * cos(ang), 1.6 * sin(ang), STANDARD_FACTION_HOSTILE, sTag);
        SetPlotFlag(a, TRUE);
        SetBaseAttackBonus(1, a);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), a);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 12), a);
        object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, a);
        if (GetIsObjectValid(w)) DestroyObject(w);
        w = GetItemInSlot(INVENTORY_SLOT_LEFTHAND, a);
        if (GetIsObjectValid(w)) DestroyObject(w);
        w = CreateItemOnObject(nKind == 2 ? "nw_wswls001" : "nw_wswdg001", a);
        SetIdentified(w, TRUE);
        if (nKind == 1) AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_2), w);
        if (nKind == 2)
        {
            AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), w);
            ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_IMPROVED_CRITICAL_LONG_SWORD), a);
        }
        AssignCommand(a, ActionEquipItem(w, INVENTORY_SLOT_RIGHTHAND));
        AssignCommand(a, ActionAttack(pc));
    }
}

void EndGroup(string sTag)
{
    int i;
    for (i = 0; i < 3; i++)
    {
        object o = GetObjectByTag(sTag, i);
        AssignCommand(o, ClearAllActions(TRUE));
        DestroyObject(o);
    }
    Mark(sTag + ".end");
}

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

    object caster = Spawn("nw_goblina", 8.0, -6.0, STANDARD_FACTION_HOSTILE, "diff_src");
    SetPlotFlag(caster, TRUE);
    DelayCommand(1.5, Mark("hostile.20"));
    DelayCommand(1.6, AssignCommand(caster, Hurt(pc, 20)));
    DelayCommand(2.5, Mark("hostile.7"));
    DelayCommand(2.6, AssignCommand(caster, Hurt(pc, 7)));
    DelayCommand(3.5, Mark("hostile.1"));
    DelayCommand(3.6, AssignCommand(caster, Hurt(pc, 1)));
    DelayCommand(4.5, Mark("missile"));
    DelayCommand(4.6, AssignCommand(caster, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, pc, METAMAGIC_ANY, TRUE)));
    DelayCommand(9.0, DestroyObject(caster));

    DelayCommand(2.0, Fight(pc));

    effect eImmune = EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT);
    DelayCommand(13.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, eImmune, pc, 23.0));
    DelayCommand(13.5, Group(pc, 0, "dagger"));
    DelayCommand(25.0, EndGroup("dagger"));
    DelayCommand(25.5, Group(pc, 1, "fire"));
    DelayCommand(37.0, EndGroup("fire"));
    DelayCommand(38.5, Group(pc, 2, "keen"));
    DelayCommand(50.0, EndGroup("keen"));
    DelayCommand(51.0, VDone());
}
