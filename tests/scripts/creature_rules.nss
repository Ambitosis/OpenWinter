// Creature rules smoke test: run with OBJECT_SELF = a creature; results go to local variables.
void main()
{
    object o = OBJECT_SELF;
    SetLocalInt(o, "str", GetAbilityScore(o, ABILITY_STRENGTH));
    SetLocalInt(o, "dexbase", GetAbilityScore(o, ABILITY_DEXTERITY, TRUE));
    SetLocalInt(o, "dexmod", GetAbilityModifier(ABILITY_DEXTERITY, o));
    SetLocalInt(o, "bab", GetBaseAttackBonus(o));
    SetLocalInt(o, "fort", GetFortitudeSavingThrow(o));
    SetLocalInt(o, "ref", GetReflexSavingThrow(o));
    SetLocalInt(o, "will", GetWillSavingThrow(o));
    SetLocalInt(o, "ac", GetAC(o));
    SetLocalInt(o, "hd", GetHitDice(o));
    SetLocalInt(o, "cls1", GetClassByPosition(1, o));
    SetLocalInt(o, "cls2", GetClassByPosition(2, o));
    SetLocalInt(o, "cls3", GetClassByPosition(3, o));
    SetLocalInt(o, "lvl2", GetLevelByPosition(2, o));
    SetLocalInt(o, "fighter", GetLevelByClass(CLASS_TYPE_FIGHTER, o));
    SetLocalInt(o, "race", GetRacialType(o));
    SetLocalInt(o, "maxhp", GetMaxHitPoints(o));
    SetLocalInt(o, "hp", GetCurrentHitPoints(o));

    // Alignment: shift toward good, then toward neutral on both axes.
    AdjustAlignment(o, ALIGNMENT_GOOD, 30, FALSE);
    SetLocalInt(o, "ge1", GetGoodEvilValue(o));
    SetLocalInt(o, "align1", GetAlignmentGoodEvil(o));
    AdjustAlignment(o, ALIGNMENT_NEUTRAL, 15, FALSE);
    SetLocalInt(o, "ge2", GetGoodEvilValue(o));
    SetLocalInt(o, "lc2", GetLawChaosValue(o));
    SetLocalInt(o, "lcalign", GetAlignmentLawChaos(o));

    // Feats and talents.
    SetLocalInt(o, "hasfeat", GetHasFeat(FEAT_WEAPON_PROFICIENCY_SIMPLE, o));
    SetLocalInt(o, "nofeat", GetHasFeat(FEAT_TURN_UNDEAD, o));
    talent t = TalentFeat(FEAT_WEAPON_PROFICIENCY_SIMPLE);
    SetLocalInt(o, "tvalid", GetIsTalentValid(t));
    SetLocalInt(o, "ttype", GetTypeFromTalent(t));
    SetLocalInt(o, "tid", GetIdFromTalent(t));
    SetLocalInt(o, "thas", GetCreatureHasTalent(t, o));

    // Immunity (the test gives the creature poison immunity) and saves that can't be failed or made.
    SetLocalInt(o, "immune", FortitudeSave(o, 10, SAVING_THROW_TYPE_POISON));
    SetLocalInt(o, "isimmune", GetIsImmune(o, IMMUNITY_TYPE_POISON));

    // Attacks per round override.
    SetBaseAttackBonus(3, o);
    SetLocalInt(o, "apr", GetAttacksPerRound(o));
    SetLocalInt(o, "aprbase", GetAttacksPerRound(o, FALSE));
    RestoreBaseAttackBonus(o);
    SetLocalInt(o, "apr2", GetAttacksPerRound(o));

    // Limits and XP.
    SetAbilityBonusLimit(4);
    SetLocalInt(o, "abilitylimit", GetAbilityBonusLimit());
    SetLocalInt(o, "xpscale", GetModuleXPScale());
    SetModuleXPScale(50);
    SetLocalInt(o, "xpscale2", GetModuleXPScale());
    int xp = GetXP(o);
    GiveXPToCreature(o, 250);
    SetLocalInt(o, "xpgain", GetXP(o) - xp);
    GiveXPToCreature(o, -100);
    SetLocalInt(o, "xpneg", GetXP(o) - xp);

    // Appearance round trips.
    SetColor(o, COLOR_CHANNEL_HAIR, 42);
    SetLocalInt(o, "hair", GetColor(o, COLOR_CHANNEL_HAIR));
    SetCreatureBodyPart(CREATURE_PART_HEAD, 7, o);
    SetLocalInt(o, "head", GetCreatureBodyPart(CREATURE_PART_HEAD, o));
    SetDeity(o, "Tymora");
    SetLocalString(o, "deity", GetDeity(o));
    SetActionMode(o, ACTION_MODE_POWER_ATTACK, TRUE);
    SetActionMode(o, ACTION_MODE_EXPERTISE, TRUE);
    SetLocalInt(o, "pa", GetActionMode(o, ACTION_MODE_POWER_ATTACK));
    SetLocalInt(o, "exp", GetActionMode(o, ACTION_MODE_EXPERTISE));
    SetLocalInt(o, "skill", GetSkillRank(SKILL_HIDE, o));
    SetLocalInt(o, "skillbase", GetSkillRank(SKILL_HIDE, o, TRUE));
    SetLocalInt(o, "done", 1);
}
