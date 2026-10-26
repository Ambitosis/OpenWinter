// NWNX_Creature level history: classes, feats, skills, hit points and ability increases by level, and levelling.
#include "parity_inc"
#include "nwnx_creature"

void Hist(string p, object o, int nMax)
{
    int i;
    string s = "";
    for (i = 1; i <= nMax; i++) s += IntToString(NWNX_Creature_GetClassByLevel(o, i)) + " ";
    PLog(p + ".classbylevel", s);
    s = "";
    for (i = 1; i <= nMax; i++)
    {
        int j, n = NWNX_Creature_GetFeatCountByLevel(o, i);
        s += IntToString(n) + ":";
        for (j = 0; j <= n; j++) s += IntToString(NWNX_Creature_GetFeatByLevel(o, i, j)) + ",";
        s += " ";
    }
    PLog(p + ".featsbylevel", s);
    s = "";
    for (i = 1; i <= nMax; i++) s += IntToString(NWNX_Creature_GetMaxHitPointsByLevel(o, i)) + " ";
    PLog(p + ".hpbylevel", s);
    s = "";
    for (i = 1; i <= nMax; i++) s += IntToString(NWNX_Creature_GetAbilityIncreaseByLevel(o, i)) + " ";
    PLog(p + ".abilitybylevel", s);
    // A blueprint creature's first entry holds whatever was in memory: from the second level on.
    s = "";
    for (i = 2; i <= nMax; i++) s += IntToString(NWNX_Creature_GetSkillPointsRemainingByLevel(o, i)) + " ";
    PLog(p + ".skillptsbylevel", s);
    s = "";
    for (i = 1; i <= nMax; i++) s += IntToString(NWNX_Creature_GetSkillRankByLevel(o, SKILL_HIDE, i)) + " ";
    PLog(p + ".hidebylevel", s);
}

void Stats(string p, object o)
{
    PLogI(p + ".hd", GetHitDice(o));
    PLog(p + ".classes", IntToString(GetClassByPosition(1, o)) + "/" + IntToString(GetLevelByPosition(1, o)) + " "
        + IntToString(GetClassByPosition(2, o)) + "/" + IntToString(GetLevelByPosition(2, o)) + " "
        + IntToString(GetClassByPosition(3, o)) + "/" + IntToString(GetLevelByPosition(3, o)));
    PLogI(p + ".bab", GetBaseAttackBonus(o));
    PLog(p + ".hp", IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o)));
    PLog(p + ".saves", IntToString(GetFortitudeSavingThrow(o)) + " " + IntToString(GetReflexSavingThrow(o)) + " " + IntToString(GetWillSavingThrow(o)));
    PLogI(p + ".featcount", NWNX_Creature_GetFeatCount(o));
    PLogI(p + ".skillpts", NWNX_Creature_GetSkillPointsRemaining(o));
    PLog(p + ".skills", IntToString(GetSkillRank(SKILL_HIDE, o, TRUE)) + " " + IntToString(GetSkillRank(SKILL_SPOT, o, TRUE)) + " " + IntToString(GetSkillRank(SKILL_DISCIPLINE, o, TRUE)));
    PLog(p + ".abilities", IntToString(GetAbilityScore(o, ABILITY_STRENGTH, TRUE)) + " " + IntToString(GetAbilityScore(o, ABILITY_DEXTERITY, TRUE)) + " " + IntToString(GetAbilityScore(o, ABILITY_CONSTITUTION, TRUE)));
    PLogI(p + ".xp", GetXP(o));
}

void main()
{
    // A blueprint creature: no level history of its own.
    object oG = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre());
    Stats("gob", oG);
    Hist("gob", oG, 2);
    PLogI("gob.grant289", NWNX_Creature_GetFeatGrantLevel(oG, 289));
    PLogI("gob.grantnone", NWNX_Creature_GetFeatGrantLevel(oG, FEAT_ALERTNESS));

    // Levels added by NWNX: the history gains their entries.
    NWNX_Creature_LevelUp(oG, CLASS_TYPE_FIGHTER, 2);
    Stats("up", oG);
    Hist("up", oG, 4);
    NWNX_Creature_LevelUp(oG, CLASS_TYPE_ROGUE, 1, PACKAGE_ROGUE);
    Stats("up2", oG);
    Hist("up2", oG, 5);
    NWNX_Creature_LevelDown(oG, 2);
    Stats("down", oG);
    Hist("down", oG, 4);

    // Feats by level on the history.
    NWNX_Creature_AddFeatByLevel(oG, FEAT_ALERTNESS, 2);
    PLogI("fbl.count2", NWNX_Creature_GetFeatCountByLevel(oG, 2));
    PLogI("fbl.grant", NWNX_Creature_GetFeatGrantLevel(oG, FEAT_ALERTNESS));
    PLogI("fbl.has", GetHasFeat(FEAT_ALERTNESS, oG));
    NWNX_Creature_RemoveFeatByLevel(oG, FEAT_ALERTNESS, 2);
    PLogI("fbl.count2b", NWNX_Creature_GetFeatCountByLevel(oG, 2));
    PLogI("fbl.hasb", GetHasFeat(FEAT_ALERTNESS, oG));
    PLogI("fbl.grantb", NWNX_Creature_GetFeatGrantLevel(oG, FEAT_ALERTNESS));

    // Setters on the history.
    NWNX_Creature_SetSkillRankByLevel(oG, SKILL_HIDE, 4, 2);
    PLogI("set.hide2", NWNX_Creature_GetSkillRankByLevel(oG, SKILL_HIDE, 2));
    PLogI("set.hiderank", GetSkillRank(SKILL_HIDE, oG, TRUE));
    PLog("set.wis0", IntToString(GetAbilityScore(oG, ABILITY_WISDOM, TRUE)) + " " + IntToString(GetAbilityScore(oG, ABILITY_STRENGTH, TRUE)));
    NWNX_Creature_SetAbilityIncreaseByLevel(oG, 2, ABILITY_WISDOM);
    PLogI("set.ab2", NWNX_Creature_GetAbilityIncreaseByLevel(oG, 2));
    PLogI("set.wis", GetAbilityScore(oG, ABILITY_WISDOM, TRUE));
    NWNX_Creature_SetAbilityIncreaseByLevel(oG, 2, ABILITY_STRENGTH);
    PLog("set.wisstr", IntToString(GetAbilityScore(oG, ABILITY_WISDOM, TRUE)) + " " + IntToString(GetAbilityScore(oG, ABILITY_STRENGTH, TRUE))
        + " " + IntToString(NWNX_Creature_GetAbilityIncreaseByLevel(oG, 2)));
    NWNX_Creature_SetAbilityIncreaseByLevel(oG, 2, 6);
    PLog("set.none", IntToString(GetAbilityScore(oG, ABILITY_STRENGTH, TRUE)) + " " + IntToString(NWNX_Creature_GetAbilityIncreaseByLevel(oG, 2)));
    NWNX_Creature_SetSkillPointsRemainingByLevel(oG, 2, 7);
    PLogI("set.sp2", NWNX_Creature_GetSkillPointsRemainingByLevel(oG, 2));
    PLogI("set.sp", NWNX_Creature_GetSkillPointsRemaining(oG));
    NWNX_Creature_SetSkillPointsRemaining(oG, 9);
    PLogI("set.spb", NWNX_Creature_GetSkillPointsRemaining(oG));
    PLogI("set.sp2b", NWNX_Creature_GetSkillPointsRemainingByLevel(oG, 2));
    int nMax = GetMaxHitPoints(oG);
    NWNX_Creature_SetMaxHitPointsByLevel(oG, 2, 3);
    PLogI("set.hp2", NWNX_Creature_GetMaxHitPointsByLevel(oG, 2));
    PLogI("set.maxdelta", GetMaxHitPoints(oG) - nMax);
    NWNX_Creature_SetMaxHitPointsByLevel(oG, 1, 20);
    PLogI("set.hp1", NWNX_Creature_GetMaxHitPointsByLevel(oG, 1));
    PLogI("set.maxdelta1", GetMaxHitPoints(oG) - nMax);

    // Position setters on a blueprint creature.
    object oB = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(2.0));
    Stats("ban", oB);
    NWNX_Creature_SetLevelByPosition(oB, 0, 6);
    Stats("lvl", oB);
    Hist("lvl", oB, 2);
    NWNX_Creature_SetClassByPosition(oB, 0, CLASS_TYPE_WIZARD);
    Stats("cls", oB);
    Hist("cls", oB, 1);
    NWNX_Creature_SetLevelByPosition(oB, 1, 3);
    Stats("lvl2", oB);

    // Position setters with a level history.
    object oH = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(4.0));
    NWNX_Creature_LevelUp(oH, CLASS_TYPE_ROGUE, 2, PACKAGE_ROGUE);
    NWNX_Creature_SetClassByPosition(oH, 1, CLASS_TYPE_BARD, TRUE);
    Stats("hcls", oH);
    Hist("hcls", oH, 3);
    NWNX_Creature_SetClassByPosition(oH, 0, CLASS_TYPE_CLERIC, FALSE);
    Stats("hcls2", oH);
    Hist("hcls2", oH, 3);
    NWNX_Creature_SetLevelByPosition(oH, 1, 5);
    Stats("hlvl", oH);
    Hist("hlvl", oH, 7);

    // A high-level blueprint creature: one history entry for twelve levels.
    object oT = CreateObject(OBJECT_TYPE_CREATURE, "nw_gnthill", PCentre(-4.0));
    Stats("hill", oT);
    Hist("hill", oT, 2);
    NWNX_Creature_LevelUp(oT, CLASS_TYPE_FIGHTER, 1);
    Stats("hillup", oT);
    Hist("hillup", oT, 3);
    NWNX_Creature_LevelDown(oT, 1);
    Stats("hilldown", oT);
    Hist("hilldown", oT, 3);
    PDone();
}
