// Levels lost through SetXP on creatures given a level history by NWNX_Creature_LevelUp and LevelUpHenchman:
// which feats go with the level, including class feats an earlier level had granted already.
#include "parity_inc"
#include "nwnx_creature"

void Read(string k, object o)
{
    PLog(k + ".levels", IntToString(GetHitDice(o)) + " " + IntToString(GetClassByPosition(1, o)) + "/" + IntToString(GetLevelByPosition(1, o))
        + " " + IntToString(GetClassByPosition(2, o)) + "/" + IntToString(GetLevelByPosition(2, o)) + " " + IntToString(GetClassByPosition(3, o)) + "/" + IntToString(GetLevelByPosition(3, o)));
    PLog(k + ".feats", IntToString(NWNX_Creature_GetFeatCount(o)) + ": " + IntToString(GetHasFeat(3, o)) + IntToString(GetHasFeat(46, o)) + IntToString(GetHasFeat(1089, o))
        + IntToString(GetHasFeat(50, o)) + IntToString(GetHasFeat(221, o)) + IntToString(GetHasFeat(2, o)));
    PLog(k + ".hp", IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o)));
    PLogI(k + ".history", NWNX_Creature_GetClassByLevel(o, GetHitDice(o)));
    PLogI(k + ".xp", GetXP(o));
}

void main()
{
    // A bandit (fighter 1) given a rogue level: the rogue level lists Light Armour Proficiency again.
    object oB = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(), FALSE, "ban");
    NWNX_Creature_LevelUp(oB, CLASS_TYPE_ROGUE, 1, PACKAGE_ROGUE);
    SetXP(oB, 1000);
    Read("ban.up", oB);
    SetXP(oB, 999);
    Read("ban.lost", oB);

    // A goblin given fighter and rogue levels, losing them one at a time and then together.
    object oG = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(2.0), FALSE, "gob");
    NWNX_Creature_LevelUp(oG, CLASS_TYPE_FIGHTER, 1);
    NWNX_Creature_LevelUp(oG, CLASS_TYPE_ROGUE, 1, PACKAGE_ROGUE);
    SetXP(oG, 3000);
    Read("gob.up", oG);
    SetXP(oG, 1000);
    Read("gob.lost1", oG);
    SetXP(oG, 0);
    Read("gob.lost2", oG);

    // LevelUpHenchman's levels.
    object oH = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(4.0), FALSE, "hen");
    SetXP(oH, 3000);
    PLogI("hen.lvl1", LevelUpHenchman(oH, CLASS_TYPE_ROGUE, FALSE, PACKAGE_ROGUE));
    PLogI("hen.lvl2", LevelUpHenchman(oH, CLASS_TYPE_ROGUE, FALSE, PACKAGE_ROGUE));
    Read("hen.up", oH);
    SetXP(oH, 999);
    Read("hen.lost", oH);
    PDone();
}
