// NWNX_Race: what racial modifiers give creatures of the race, parent races, and suppressing the race's effects.
#include "parity_inc"
#include "nwnx_race"
#include "nwnx_effect"
#include "nwnx_creature"

string O(object o) { return o == OBJECT_INVALID ? "inv" : GetIsObjectValid(o) ? GetTag(o) : "other"; }

void DumpTrue(string k, object o)
{
    int n = NWNX_Effect_GetTrueEffectCount(o);
    int i;
    for (i = 0; i < n; i++)
    {
        struct NWNX_EffectUnpacked u = NWNX_Effect_GetTrueEffect(o, i);
        string sInts = IntToString(u.nParam0) + "," + IntToString(u.nParam1);
        if (u.nType != 7) sInts += "," + IntToString(u.nParam2) + "," + IntToString(u.nParam3) + "," + IntToString(u.nParam4) + "," + IntToString(u.nParam5);
        PLog(k + "." + IntToString(i), IntToString(u.nType) + "/" + IntToString(u.nSubType) + ":" + sInts
            + ":" + O(u.oCreator) + ":" + IntToString(u.nSpellId) + ":" + u.sTag + ":" + IntToString(u.bExpose) + "/" + IntToString(u.bShowIcon));
    }
    PLogI(k + ".count", n);
}

void Stats(string k, object o)
{
    PLogI(k + ".ac", GetAC(o));
    PLog(k + ".saves", IntToString(GetFortitudeSavingThrow(o)) + "/" + IntToString(GetReflexSavingThrow(o)) + "/" + IntToString(GetWillSavingThrow(o)));
    PLog(k + ".immune", IntToString(GetIsImmune(o, IMMUNITY_TYPE_FEAR)) + "/" + IntToString(GetIsImmune(o, IMMUNITY_TYPE_POISON)));
    PLogI(k + ".sr", GetSpellResistance(o));
    // Skill ranks are compared as changes (the feats a race grants), not as totals.
    WriteTimestampedLogEntry("RAW|" + k + ".skills|" + IntToString(GetSkillRank(SKILL_SPOT, o)) + "/" + IntToString(GetSkillRank(SKILL_LISTEN, o)) + "/" + IntToString(GetSkillRank(SKILL_SPOT, o, TRUE)));
    PLog(k + ".feats", IntToString(GetHasFeat(FEAT_IRON_WILL, o)) + "/" + IntToString(GetHasFeat(FEAT_TOUGHNESS, o)));
    PLogI(k + ".race", GetRacialType(o));
}

void Later2(object oNew)
{
    PLogI("new2s.count", NWNX_Effect_GetTrueEffectCount(oNew));
    object oHench = GetObjectByTag("RC_HENCH");
    // A non-player's level-up brings no racial effects (whatever its new level, for which see the levelling suites).
    PLogI("hench.count", NWNX_Effect_GetTrueEffectCount(oHench));
    PDone();
}

void Later1()
{
    PLogI("gob1s.count", NWNX_Effect_GetTrueEffectCount(GetObjectByTag("RC_GNOLL")));
    object oNew = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(6.0), FALSE, "RC_NEW");
    PLogI("new.count", NWNX_Effect_GetTrueEffectCount(oNew));
    object oHench = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-6.0), FALSE, "RC_HENCH");
    int i;
    for (i = 0; i < 5; i++) LevelUpHenchman(oHench, CLASS_TYPE_INVALID, TRUE);
    DelayCommand(1.0, Later2(oNew));
}

void main()
{
    int nRace = RACIAL_TYPE_HUMANOID_GOBLINOID;
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_AB, 2);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_AC, 3, AC_DODGE_BONUS);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_AC, 1, AC_NATURAL_BONUS);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_ABVSRACE, RACIAL_TYPE_ELF, 3);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_ACVSRACE, RACIAL_TYPE_ELF, 4);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_CONCEALMENT, 20);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_DMGIMMUNITY, DAMAGE_TYPE_COLD, 50);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_DMGREDUCTION, 5, 2);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_DMGRESIST, DAMAGE_TYPE_FIRE, 10);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_IMMUNITY, IMMUNITY_TYPE_FEAR);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_MOVEMENTSPEED, 25);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_REGENERATION, 2, 6);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_SAVE, SAVING_THROW_FORT, 2);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_SAVEVSRACE, SAVING_THROW_ALL, RACIAL_TYPE_UNDEAD, 2);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_SAVEVSTYPE, SAVING_THROW_WILL, SAVING_THROW_TYPE_FEAR, 3);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_SKILL, SKILL_SPOT, 4);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_SPELLIMMUNITY, SPELL_MAGIC_MISSILE);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_SRCHARGEN, 10, 30);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_SRINCLEVEL, 1, 1, 1);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_FEAT, FEAT_IRON_WILL, 1);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_FEAT, FEAT_TOUGHNESS, 5);
    NWNX_Race_SetRacialModifier(nRace, NWNX_RACE_MODIFIER_INITIATIVE, 3);
    // A parent race for gnolls (13): goblinoid.
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_HUMANOID_MONSTROUS, NWNX_RACE_MODIFIER_RACE, RACIAL_TYPE_HUMANOID_GOBLINOID);
    NWNX_Race_SetFavoredEnemyFeat(RACIAL_TYPE_HUMANOID_MONSTROUS, FEAT_FAVORED_ENEMY_GOBLINOID);

    // Several modifiers of one kind for animals (8).
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_AB, 1);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_AB, 2);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_AC, 2, AC_DODGE_BONUS);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_AC, 1, AC_DEFLECTION_BONUS);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_DMGRESIST, DAMAGE_TYPE_FIRE, 10);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_DMGRESIST, DAMAGE_TYPE_COLD, 5);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_SAVE, SAVING_THROW_FORT, 2);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_IMMUNITY, IMMUNITY_TYPE_FEAR);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_IMMUNITY, IMMUNITY_TYPE_POISON);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_SKILL, SKILL_SPOT, 4);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_SKILL, SKILL_LISTEN, -2);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_MOVEMENTSPEED, -20);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_CONCEALMENT, 10);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_CONCEALMENT, 30);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_SRCHARGEN, 5);
    NWNX_Race_SetRacialModifier(RACIAL_TYPE_ANIMAL, NWNX_RACE_MODIFIER_FEAT, FEAT_ALERTNESS, 1);

    PLog("parent", IntToString(NWNX_Race_GetParentRace(RACIAL_TYPE_HUMANOID_MONSTROUS)) + "/" + IntToString(NWNX_Race_GetParentRace(RACIAL_TYPE_HUMANOID_GOBLINOID)) + "/" + IntToString(NWNX_Race_GetParentRace(RACIAL_TYPE_ELF)));

    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(), FALSE, "RC_GOB");
    DumpTrue("gob", oGob);
    Stats("gob", oGob);
    object oPlain = CreateObject(OBJECT_TYPE_CREATURE, "nw_badger", PCentre(3.0), FALSE, "RC_PLAIN");
    DumpTrue("plain", oPlain);
    Stats("plain", oPlain);
    object oGnoll = CreateObject(OBJECT_TYPE_CREATURE, "nw_gnoll001", PCentre(-3.0), FALSE, "RC_GNOLL");
    PLogI("gnoll.race", GetRacialType(oGnoll));
    DumpTrue("gnoll", oGnoll);

    NWNX_Race_SuppressCreatureRaceEffects(oGob);
    DumpTrue("suppressed", oGob);
    Stats("suppressed", oGob);
    NWNX_Race_ReactivateCreatureRaceEffects(oGob);
    DumpTrue("reactivated", oGob);
    NWNX_Race_ReactivateCreatureRaceEffects(oGob);
    PLogI("reactivated2.count", NWNX_Effect_GetTrueEffectCount(oGob));

    object oAnimal = CreateObject(OBJECT_TYPE_CREATURE, "nw_badger", PCentre(0.0, 5.0), FALSE, "RC_ANIMAL");
    Stats("animal", oAnimal);
    int nSpot = GetSkillRank(SKILL_SPOT, oAnimal), nListen = GetSkillRank(SKILL_LISTEN, oAnimal);
    NWNX_Race_ReactivateCreatureRaceEffects(oAnimal);
    PLog("animal.skillchange", IntToString(GetSkillRank(SKILL_SPOT, oAnimal) - nSpot) + "/" + IntToString(GetSkillRank(SKILL_LISTEN, oAnimal) - nListen));
    DumpTrue("animal", oAnimal);
    Stats("animalfx", oAnimal);
    Stats("reactivated", oGob);

    // A creature that becomes the race.
    NWNX_Creature_SetRacialType(oPlain, nRace);
    DumpTrue("changed", oPlain);
    DelayCommand(1.0, Later1());
}
