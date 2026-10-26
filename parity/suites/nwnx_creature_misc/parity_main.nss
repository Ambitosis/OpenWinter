// NWNX_Creature odds and ends: personal space and body sizes, body bag, encounter, bartering, orientation lock,
// store price limit, multiclass limit, killer, associates, feat requirements, immunity bypass, skills, limbo.
#include "parity_inc"
#include "nwnx_creature"

int HasEffectType(int nType, object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectType(e) == nType) return TRUE; e = GetNextEffect(o); }
    return FALSE;
}

void Sizes(string k, object o)
{
    PLog(k + ".sizes", FloatToString(NWNX_Creature_GetPersonalSpace(o), 0, 3) + " " + FloatToString(NWNX_Creature_GetCreaturePersonalSpace(o), 0, 3)
        + " " + FloatToString(NWNX_Creature_GetHeight(o), 0, 3) + " " + FloatToString(NWNX_Creature_GetHitDistance(o), 0, 3)
        + " " + FloatToString(NWNX_Creature_GetPreferredAttackDistance(o), 0, 3));
}

void Facing(object o, object t)
{
    PLogF("lock.facing", GetFacing(o), 3.0);
    PLogI("lock.still", NWNX_Creature_GetLockOrientationToObject(o) == t);
}

void Later(object oX, object oY, object oG, object oBy)
{
    PLogI("limbo.valid", GetIsObjectValid(oY));
    PLogI("limbo.area", GetIsObjectValid(GetArea(oY)));
    PLogI("limbo.nearest", GetNearestObjectByTag("limbo_me", oX) == oY);
    PLogI("bypass.stunned", HasEffectType(EFFECT_TYPE_STUNNED, oBy));
    PDone();
}

void main()
{
    object oB = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(), FALSE, "ban");
    object oG = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(4.0), FALSE, "gob");
    object oD = CreateObject(OBJECT_TYPE_CREATURE, "nw_drgred001", PCentre(-8.0, 8.0), FALSE, "drg");
    Sizes("ban", oB);
    Sizes("gob", oG);
    Sizes("drg", oD);
    PLogF("drg.range", NWNX_Creature_GetMaxAttackRange(oD, oB));
    PLogF("ban.rangedrg", NWNX_Creature_GetMaxAttackRange(oB, oD));
    NWNX_Creature_SetPersonalSpace(oG, 0.9);
    NWNX_Creature_SetCreaturePersonalSpace(oG, 0.8);
    NWNX_Creature_SetHeight(oG, 2.5);
    NWNX_Creature_SetHitDistance(oG, 1.5);
    NWNX_Creature_SetPreferredAttackDistance(oG, 2.25);
    Sizes("gobset", oG);
    PLogF("gobset.range", NWNX_Creature_GetMaxAttackRange(oB, oG));
    PLogF("gobset.rangeout", NWNX_Creature_GetMaxAttackRange(oG, oB));
    Sizes("invalid", OBJECT_INVALID);

    PLogI("bodybag", NWNX_Creature_GetBodyBag(oB) == OBJECT_INVALID);
    PLogI("encounter", NWNX_Creature_GetEncounter(oB) == OBJECT_INVALID);
    PLogI("bartering", NWNX_Creature_GetIsBartering(oB));
    PLogI("multiclass", NWNX_Creature_GetMulticlassLimit(oB));
    NWNX_Creature_SetMulticlassLimit(oB, 2);
    PLogI("multiclass.set", NWNX_Creature_GetMulticlassLimit(oB));

    object oS = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", PCentre(-4.0));
    PLogI("store.default", NWNX_Creature_GetMaxSellToStorePriceOverride(oB, oS));
    NWNX_Creature_SetMaxSellToStorePriceOverride(oB, oS, 100);
    PLogI("store.set", NWNX_Creature_GetMaxSellToStorePriceOverride(oB, oS));
    PLogI("store.other", NWNX_Creature_GetMaxSellToStorePriceOverride(oG, oS));
    NWNX_Creature_SetMaxSellToStorePriceOverride(oB, oS, -1);
    PLogI("store.none", NWNX_Creature_GetMaxSellToStorePriceOverride(oB, oS));
    NWNX_Creature_SetMaxSellToStorePriceOverride(oB, oS, -2);
    PLogI("store.removed", NWNX_Creature_GetMaxSellToStorePriceOverride(oB, oS));

    // Associates and their names.
    PLog("assoc.default", IntToString(GetFamiliarCreatureType(oB)) + " " + IntToString(GetAnimalCompanionCreatureType(oB)) + " [" + GetFamiliarName(oB) + "] [" + GetAnimalCompanionName(oB) + "]");
    NWNX_Creature_SetFamiliarCreatureType(oB, FAMILIAR_CREATURE_TYPE_PIXIE);
    NWNX_Creature_SetAnimalCompanionCreatureType(oB, ANIMAL_COMPANION_CREATURE_TYPE_WOLF);
    NWNX_Creature_SetFamiliarName(oB, "Pip");
    NWNX_Creature_SetAnimalCompanionName(oB, "Fang");
    PLog("assoc.set", IntToString(GetFamiliarCreatureType(oB)) + " " + IntToString(GetAnimalCompanionCreatureType(oB)) + " [" + GetFamiliarName(oB) + "] [" + GetAnimalCompanionName(oB) + "]");

    // An associate added directly.
    object oH = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-6.0, 4.0), FALSE, "hench");
    NWNX_Creature_AddAssociate(oB, oH, ASSOCIATE_TYPE_HENCHMAN);
    PLog("assoc.added", IntToString(GetMaster(oH) == oB) + " " + IntToString(GetAssociateType(oH)) + " "
        + IntToString(GetHenchman(oB) == oH) + " " + IntToString(GetFactionEqual(oB, oH)) + " " + IntToString(GetAssociate(ASSOCIATE_TYPE_HENCHMAN, oB) == oH));

    // A safe spot near a point on open floor.
    vector vSafe = NWNX_Creature_ComputeSafeLocation(oB, Vector(23.0, 21.0, 0.0), 5.0, FALSE);
    PLog("safe.floor", FloatToString(vSafe.x, 0, 1) + " " + FloatToString(vSafe.y, 0, 1) + " " + FloatToString(vSafe.z, 0, 1));

    // Killer.
    ExecuteScript("p_lk", oB);
    NWNX_Creature_SetLastKiller(oB, oG);
    ExecuteScript("p_lk", oB);

    // Feat requirements.
    PLog("feats.meets", IntToString(NWNX_Creature_GetMeetsFeatRequirements(oB, FEAT_POWER_ATTACK)) + " " + IntToString(NWNX_Creature_GetMeetsFeatRequirements(oB, FEAT_CLEAVE))
        + " " + IntToString(NWNX_Creature_GetMeetsFeatRequirements(oB, FEAT_GREAT_CLEAVE)) + " " + IntToString(NWNX_Creature_GetMeetsFeatRequirements(oB, FEAT_WEAPON_SPECIALIZATION_LONG_SWORD))
        + " " + IntToString(NWNX_Creature_GetMeetsFeatRequirements(oB, FEAT_ALERTNESS)) + " " + IntToString(NWNX_Creature_GetMeetsFeatRequirements(oG, FEAT_POWER_ATTACK))
        + " " + IntToString(NWNX_Creature_GetMeetsFeatRequirements(oB, FEAT_EPIC_PROWESS)) + " " + IntToString(NWNX_Creature_GetMeetsFeatRequirements(oB, FEAT_WEAPON_FOCUS_LONG_SWORD))
        + " " + IntToString(NWNX_Creature_GetMeetsFeatRequirements(oB, FEAT_EXTRA_TURNING)) + " " + IntToString(NWNX_Creature_GetMeetsFeatRequirements(oB, FEAT_DODGE)));

    // Skills set directly.
    PLog("skill.start", IntToString(GetSkillRank(SKILL_HIDE, oB, TRUE)) + " " + IntToString(GetSkillRank(SKILL_HIDE, oB)) + " " + IntToString(GetSkillRank(SKILL_DISCIPLINE, oB)));
    NWNX_Creature_SetSkillRank(oB, SKILL_HIDE, 12);
    NWNX_Creature_SetSkillRank(oB, SKILL_DISCIPLINE, 0);
    PLog("skill.set", IntToString(GetSkillRank(SKILL_HIDE, oB, TRUE)) + " " + IntToString(GetSkillRank(SKILL_HIDE, oB)) + " " + IntToString(GetSkillRank(SKILL_DISCIPLINE, oB))
        + " " + IntToString(GetHasSkill(SKILL_HIDE, oB)));
    NWNX_Creature_SetSkillRank(oB, SKILL_HIDE, 200);
    PLogI("skill.200", GetSkillRank(SKILL_HIDE, oB, TRUE));
    NWNX_Creature_SetSkillRank(oB, SKILL_HIDE, -5);
    PLogI("skill.neg", GetSkillRank(SKILL_HIDE, oB, TRUE));
    PLogI("skill.check", GetIsSkillSuccessful(oB, SKILL_HIDE, 60));
    NWNX_Creature_SetSkillRank(oB, SKILL_HIDE, 127);
    PLogI("skill.check127", GetIsSkillSuccessful(oB, SKILL_HIDE, 60));

    // Abilities.
    NWNX_Creature_SetRawAbilityScore(oB, ABILITY_STRENGTH, 20);
    PLog("ab.set", IntToString(GetAbilityScore(oB, ABILITY_STRENGTH, TRUE)) + " " + IntToString(NWNX_Creature_GetRawAbilityScore(oB, ABILITY_STRENGTH)));
    NWNX_Creature_ModifyRawAbilityScore(oB, ABILITY_STRENGTH, -4);
    PLog("ab.mod", IntToString(GetAbilityScore(oB, ABILITY_STRENGTH, TRUE)) + " " + IntToString(NWNX_Creature_GetRawAbilityScore(oB, ABILITY_STRENGTH)));
    PLog("ab.gob", IntToString(GetAbilityScore(oG, ABILITY_STRENGTH, TRUE)) + " " + IntToString(NWNX_Creature_GetRawAbilityScore(oG, ABILITY_STRENGTH))
        + " " + IntToString(NWNX_Creature_GetPrePolymorphAbilityScore(oG, ABILITY_STRENGTH)));

    // Immunity bypass: an attacker that ignores stun immunity.
    object oBy = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(0.0, -4.0), FALSE, "byp");
    object oAt = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(2.0, -4.0), FALSE, "att");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_STUN), oBy);
    PLogI("bypass.default", NWNX_Creature_GetBypassEffectImmunity(oAt, IMMUNITY_TYPE_STUN));
    NWNX_Creature_SetBypassEffectImmunity(oAt, IMMUNITY_TYPE_STUN, 100);
    NWNX_Creature_SetBypassEffectImmunity(oBy, -IMMUNITY_TYPE_STUN, 40);
    PLog("bypass.read", IntToString(NWNX_Creature_GetBypassEffectImmunity(oAt, IMMUNITY_TYPE_STUN)) + " " + IntToString(NWNX_Creature_GetBypassEffectImmunity(oAt, -IMMUNITY_TYPE_STUN))
        + " " + IntToString(NWNX_Creature_GetBypassEffectImmunity(oBy, -IMMUNITY_TYPE_STUN)) + " " + IntToString(NWNX_Creature_GetBypassEffectImmunity(oBy, IMMUNITY_TYPE_STUN)));
    NWNX_Creature_SetBypassEffectImmunity(oBy, -IMMUNITY_TYPE_STUN, 0);
    PLogI("bypass.immune", GetIsImmune(oBy, IMMUNITY_TYPE_STUN, oAt));
    AssignCommand(oAt, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectStunned(), oBy, 30.0));

    // Orientation lock: the creature turns to keep facing its target.
    NWNX_Creature_SetLockOrientationToObject(oB, oG);
    PLogI("lock.get", NWNX_Creature_GetLockOrientationToObject(oB) == oG);
    PLogI("lock.none", NWNX_Creature_GetLockOrientationToObject(oG) == OBJECT_INVALID);
    AssignCommand(oG, JumpToLocation(PCentre(0.0, 4.0)));
    DelayCommand(1.0, Facing(oB, oG));

    // Limbo.
    object oY = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(6.0, -4.0), FALSE, "limbo_me");
    NWNX_Creature_JumpToLimbo(oY);
    DelayCommand(1.5, Later(oB, oY, oG, oBy));
}
