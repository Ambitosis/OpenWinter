// NWNX_Creature attack figures: base attack bonus, attacks per round, attack bonus, effect bonuses, armour class
// versus, check penalties, attack range, bonus attack limit, critical and initiative settings.
#include "parity_inc"
#include "nwnx_creature"

void Ab(string k, object o)
{
    PLog(k + ".ab", IntToString(NWNX_Creature_GetAttackBonus(o)) + " " + IntToString(NWNX_Creature_GetAttackBonus(o, TRUE)) + " "
        + IntToString(NWNX_Creature_GetAttackBonus(o, FALSE)) + " " + IntToString(NWNX_Creature_GetAttackBonus(o, TRUE, TRUE)) + " "
        + IntToString(NWNX_Creature_GetAttackBonus(o, TRUE, FALSE, TRUE)) + " " + IntToString(NWNX_Creature_GetAttackBonus(o, TRUE, FALSE, FALSE, FALSE)));
    PLog(k + ".apr", IntToString(NWNX_Creature_GetAttacksPerRound(o)) + " " + IntToString(NWNX_Creature_GetAttacksPerRound(o, TRUE)));
    PLogI(k + ".bab", GetBaseAttackBonus(o));
}

void Teb(string k, object o, object t)
{
    PLog(k + ".teb", IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_ATTACK, t)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_DAMAGE, t)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_DAMAGE, t, TRUE)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_DAMAGE, t, TRUE, TRUE)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_SAVING_THROW, t, 0, 0, SAVING_THROW_FORT)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_SAVING_THROW, t, 0, 0, SAVING_THROW_WILL, SAVING_THROW_TYPE_FEAR)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_SAVING_THROW, t, 0, 0, SAVING_THROW_ALL)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_ABILITY, t, 0, 0, -1, -1, -1, ABILITY_STRENGTH)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_ABILITY, t, 0, 0, -1, -1, -1, ABILITY_DEXTERITY)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_SKILL, t, 0, 0, -1, -1, SKILL_HIDE)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_SKILL, t, 0, 0, -1, -1, SKILL_SPOT)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_TOUCH_ATTACK, t)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(o, NWNX_CREATURE_BONUS_TYPE_ATTACK, t, 0, 0, -1, -1, -1, -1, TRUE)));
}

void Ac(string k, object o, object v)
{
    PLog(k + ".acv", IntToString(NWNX_Creature_GetArmorClassVersus(o, OBJECT_INVALID)) + " " + IntToString(NWNX_Creature_GetArmorClassVersus(o, v))
        + " " + IntToString(NWNX_Creature_GetArmorClassVersus(o, v, TRUE)) + " " + IntToString(NWNX_Creature_GetArmorClassVersus(o, OBJECT_INVALID, TRUE))
        + " ac=" + IntToString(GetAC(o)));
    PLog(k + ".pen", IntToString(NWNX_Creature_GetArmorCheckPenalty(o)) + " " + IntToString(NWNX_Creature_GetShieldCheckPenalty(o)));
    PLogF(k + ".range", NWNX_Creature_GetMaxAttackRange(o, v));
    PLogI(k + ".flat", NWNX_Creature_GetFlatFooted(o));
}

void main()
{
    object oF = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre());
    object oG = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(1.5));
    object oN = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(-3.0));
    Ab("bare", oF);
    Ac("bare", oF, oG);
    Ac("gob", oG, oF);
    Teb("bare", oF, oG);

    // Base attack bonus override, and the scripted attack count override on top.
    NWNX_Creature_SetBaseAttackBonus(oF, 11);
    Ab("bab11", oF);
    SetBaseAttackBonus(4, oF);
    Ab("bab11apr4", oF);
    RestoreBaseAttackBonus(oF);
    NWNX_Creature_SetBaseAttackBonus(oF, 0);
    Ab("bab0", oF);
    NWNX_Creature_SetBaseAttackBonus(oF, 16);
    Ab("bab16", oF);
    NWNX_Creature_SetBaseAttackBonus(oF, 0);

    // Weapons and armour, equipped instantly.
    object oSword = CreateItemOnObject("nw_wswls001", oF);
    object oBow = CreateItemOnObject("nw_wbwln001", oF);
    object oArmour = CreateItemOnObject("nw_aarcl004", oF);
    object oShield = CreateItemOnObject("nw_ashto001", oF);
    object oDagger = CreateItemOnObject("nw_wswdg001", oF);
    PLogI("eq.armour", NWNX_Creature_RunEquip(oF, oArmour, INVENTORY_SLOT_CHEST));
    PLogI("eq.armourin", GetItemInSlot(INVENTORY_SLOT_CHEST, oF) == oArmour);
    PLogI("eq.sword", NWNX_Creature_RunEquip(oF, oSword, INVENTORY_SLOT_RIGHTHAND));
    PLogI("eq.shield", NWNX_Creature_RunEquip(oF, oShield, INVENTORY_SLOT_LEFTHAND));
    PLogI("eq.wrongslot", NWNX_Creature_RunEquip(oF, oDagger, INVENTORY_SLOT_HEAD));
    PLogI("eq.notmine", NWNX_Creature_RunEquip(oF, GetItemInSlot(INVENTORY_SLOT_CARMOUR, oG), INVENTORY_SLOT_RIGHTHAND));
    Ab("sword", oF);
    Ac("sword", oF, oG);
    PLogI("eq.unshield", NWNX_Creature_RunUnequip(oF, oShield));
    PLogI("eq.shieldin", GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_LEFTHAND, oF)));
    PLogI("eq.dagger", NWNX_Creature_RunEquip(oF, oDagger, INVENTORY_SLOT_LEFTHAND));
    Ab("dual", oF);
    PLogI("eq.unnone", NWNX_Creature_RunUnequip(oF, oShield));
    PLogI("eq.bow", NWNX_Creature_RunEquip(oF, oBow, INVENTORY_SLOT_RIGHTHAND));
    PLogI("eq.daggerafterbow", GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_LEFTHAND, oF)));
    Ab("bow", oF);
    Ac("bow", oF, oG);

    // Effect bonuses.
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(3), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(2), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusRacialTypeEffect(EffectAttackIncrease(4), RACIAL_TYPE_HUMANOID_GOBLINOID), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(1, ATTACK_BONUS_OFFHAND), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageIncrease(DAMAGE_BONUS_2, DAMAGE_TYPE_SLASHING), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageIncrease(DAMAGE_BONUS_3, DAMAGE_TYPE_FIRE), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowIncrease(SAVING_THROW_FORT, 2), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowIncrease(SAVING_THROW_ALL, 1), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowIncrease(SAVING_THROW_WILL, 3, SAVING_THROW_TYPE_FEAR), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 4), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_DEXTERITY, 2), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_HIDE, 5), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillDecrease(SKILL_SPOT, 3), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2), oN);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusRacialTypeEffect(EffectACIncrease(3), RACIAL_TYPE_HUMANOID_GOBLINOID), oN);
    Teb("fx", oN, OBJECT_INVALID);
    Teb("fxgob", oN, oG);
    Ab("fx", oN);
    Ac("fx", oN, oG);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackDecrease(10), oN);
    Teb("fxdec", oN, OBJECT_INVALID);
    Ab("fxdec", oN);

    // Damage dice: only their maximum is deterministic.
    object oE = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(-5.0));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageIncrease(DAMAGE_BONUS_1d6, DAMAGE_TYPE_COLD), oE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageIncrease(DAMAGE_BONUS_2d4, DAMAGE_TYPE_PIERCING), oE);
    PLog("dice.max", IntToString(NWNX_Creature_GetTotalEffectBonus(oE, NWNX_CREATURE_BONUS_TYPE_DAMAGE, OBJECT_INVALID, FALSE, TRUE)) + " "
        + IntToString(NWNX_Creature_GetTotalEffectBonus(oE, NWNX_CREATURE_BONUS_TYPE_DAMAGE, OBJECT_INVALID, TRUE, TRUE)));

    // Bonus attack limit.
    object oM = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(3.0));
    PLogI("mba.default", NWNX_Creature_GetMaximumBonusAttacks(oM));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectModifyAttacks(5), oM);
    Ab("mba5", oM);
    NWNX_Creature_SetMaximumBonusAttacks(oM, 2);
    PLogI("mba.set2", NWNX_Creature_GetMaximumBonusAttacks(oM));
    Ab("mba2", oM);
    NWNX_Creature_SetMaximumBonusAttacks(oM, 8);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectModifyAttacks(5), oM);
    PLogI("mba.set8", NWNX_Creature_GetMaximumBonusAttacks(oM));
    Ab("mba8", oM);
    NWNX_Creature_SetMaximumBonusAttacks(oM, -1);
    PLogI("mba.reset", NWNX_Creature_GetMaximumBonusAttacks(oM));
    Ab("mbareset", oM);

    // Critical and initiative settings read back.
    object oC = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(5.0));
    PLog("crit.default", IntToString(NWNX_Creature_GetCriticalMultiplierModifier(oC)) + " " + IntToString(NWNX_Creature_GetCriticalMultiplierOverride(oC))
        + " " + IntToString(NWNX_Creature_GetCriticalRangeModifier(oC)) + " " + IntToString(NWNX_Creature_GetCriticalRangeOverride(oC)));
    NWNX_Creature_SetCriticalMultiplierModifier(oC, 2);
    NWNX_Creature_SetCriticalMultiplierModifier(oC, 1, 1);
    NWNX_Creature_SetCriticalMultiplierModifier(oC, 3, 2, FALSE, BASE_ITEM_DAGGER);
    NWNX_Creature_SetCriticalMultiplierOverride(oC, 4, 1);
    NWNX_Creature_SetCriticalRangeModifier(oC, -2, 0, FALSE, BASE_ITEM_LONGSWORD);
    NWNX_Creature_SetCriticalRangeOverride(oC, 15, 2);
    PLog("crit.mult", IntToString(NWNX_Creature_GetCriticalMultiplierModifier(oC)) + " " + IntToString(NWNX_Creature_GetCriticalMultiplierModifier(oC, 1))
        + " " + IntToString(NWNX_Creature_GetCriticalMultiplierModifier(oC, 2)) + " " + IntToString(NWNX_Creature_GetCriticalMultiplierModifier(oC, 2, BASE_ITEM_DAGGER))
        + " " + IntToString(NWNX_Creature_GetCriticalMultiplierModifier(oC, 0, BASE_ITEM_DAGGER)) + " " + IntToString(NWNX_Creature_GetCriticalMultiplierModifier(oC, 3)));
    PLog("crit.multo", IntToString(NWNX_Creature_GetCriticalMultiplierOverride(oC)) + " " + IntToString(NWNX_Creature_GetCriticalMultiplierOverride(oC, 1))
        + " " + IntToString(NWNX_Creature_GetCriticalMultiplierOverride(oC, 2)));
    PLog("crit.range", IntToString(NWNX_Creature_GetCriticalRangeModifier(oC)) + " " + IntToString(NWNX_Creature_GetCriticalRangeModifier(oC, 0, BASE_ITEM_LONGSWORD))
        + " " + IntToString(NWNX_Creature_GetCriticalRangeModifier(oC, 1, BASE_ITEM_LONGSWORD)) + " " + IntToString(NWNX_Creature_GetCriticalRangeOverride(oC, 2))
        + " " + IntToString(NWNX_Creature_GetCriticalRangeOverride(oC, 0)) + " " + IntToString(NWNX_Creature_GetCriticalRangeOverride(oC, 1)));
    NWNX_Creature_SetCriticalRangeOverride(oC, -1, 2);
    NWNX_Creature_SetCriticalMultiplierModifier(oC, 0);
    PLog("crit.clear", IntToString(NWNX_Creature_GetCriticalRangeOverride(oC, 2)) + " " + IntToString(NWNX_Creature_GetCriticalMultiplierModifier(oC)));
    PLogI("init.default", NWNX_Creature_GetInitiativeModifier(oC));
    NWNX_Creature_SetInitiativeModifier(oC, 4);
    PLogI("init.set", NWNX_Creature_GetInitiativeModifier(oC));
    PLogI("flank", NWNX_Creature_GetIsFlanking(oF, oG));
    PLogI("invalid.apr", NWNX_Creature_GetAttacksPerRound(OBJECT_INVALID));
    PLogI("invalid.ab", NWNX_Creature_GetAttackBonus(OBJECT_INVALID));
    PLogI("invalid.acv", NWNX_Creature_GetArmorClassVersus(OBJECT_INVALID, oF));
    PLogI("invalid.mba", NWNX_Creature_GetMaximumBonusAttacks(OBJECT_INVALID));
    PLogI("invalid.teb", NWNX_Creature_GetTotalEffectBonus(OBJECT_INVALID));
    PLogI("invalid.pen", NWNX_Creature_GetArmorCheckPenalty(OBJECT_INVALID));
    PLogF("invalid.range", NWNX_Creature_GetMaxAttackRange(OBJECT_INVALID, oF));
    PLogI("invalid.crit", NWNX_Creature_GetCriticalRangeOverride(OBJECT_INVALID));
    PDone();
}
