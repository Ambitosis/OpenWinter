#include "ep_inc"
// encounter_events (docs/ENCOUNTERS.md): encounters on a server with no player. Seven encounters (suite.json, the
// stock goblin group with event scripts, Prelude's factions) and a walker (scripts stripped) for each that walks
// in and out. Logs the encounter functions (Get/SetEncounterActive, SpawnsMax/Current, Difficulty), every
// OnEntered / OnExit / OnExhausted in order, whether OnHeartbeat ran, and the encounter creatures near each one:
// the original spawns nothing without a player in the area, even for hostile NPCs with PlayerOnly off.

object Walker(string sEnc, int nFaction, int bInside)
{
    object oEnc = EpEnc(sEnc);
    vector c = GetPosition(oEnc);
    location lStart = Location(EpArea(), Vector(c.x, c.y - (bInside ? 0.0 : 8.0), 0.0), 90.0);
    object o = CreateObject(OBJECT_TYPE_CREATURE, "ep_walk", lStart, FALSE, "W_" + sEnc);
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    if (!bInside)
    {
        AssignCommand(o, ActionMoveToLocation(Location(EpArea(), Vector(c.x, c.y, 0.0), 90.0)));
        AssignCommand(o, ActionWait(4.0));
        AssignCommand(o, ActionMoveToLocation(Location(EpArea(), Vector(c.x, c.y - 8.0, 0.0), 90.0)));
    }
    return o;
}

void Report(string sLabel)
{
    int i;
    for (i = 1; i <= 7; i++)
    {
        object oEnc = EpEnc("E" + IntToString(i));
        EpReport(oEnc);
        EpCount(oEnc, sLabel);
    }
}

void KillE1()
{
    object o = GetFirstObjectInArea(EpArea());
    while (GetIsObjectValid(o))
    {
        if (GetObjectType(o) == OBJECT_TYPE_CREATURE && GetIsEncounterCreature(o) && GetNearestObject(OBJECT_TYPE_ENCOUNTER, o) == EpEnc("E1"))
        {
            SetPlotFlag(o, FALSE);
            ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
        }
        o = GetNextObjectInArea(EpArea());
    }
}

void main()
{
    int i;
    for (i = 1; i <= 7; i++) EpReport(EpEnc("E" + IntToString(i)));
    PLog("walker.level", IntToString(GetHitDice(CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", Location(EpArea(), Vector(75.0, 75.0, 0.0), 0.0)))));
    Walker("E1", STANDARD_FACTION_COMMONER, FALSE);
    Walker("E2", STANDARD_FACTION_HOSTILE, FALSE);
    Walker("E3", STANDARD_FACTION_HOSTILE, FALSE);
    Walker("E4", STANDARD_FACTION_HOSTILE, FALSE);
    object o5 = Walker("E5", STANDARD_FACTION_COMMONER, TRUE);
    AssignCommand(o5, ActionMoveToLocation(Location(EpArea(), GetPosition(EpEnc("E5")) + Vector(1.0, 1.0, 0.0), 0.0)));
    object oE7 = EpEnc("E7");
    SetEncounterDifficulty(ENCOUNTER_DIFFICULTY_HARD, oE7); PLogI("set.difficulty.hard", GetEncounterDifficulty(oE7));
    SetEncounterDifficulty(7, oE7); PLogI("set.difficulty.7", GetEncounterDifficulty(oE7));
    SetEncounterDifficulty(-1, oE7); PLogI("set.difficulty.-1", GetEncounterDifficulty(oE7));
    SetEncounterDifficulty(ENCOUNTER_DIFFICULTY_EASY, oE7); PLogI("set.difficulty.easy", GetEncounterDifficulty(oE7));
    SetEncounterSpawnsMax(5, oE7); PLogI("set.max", GetEncounterSpawnsMax(oE7));
    SetEncounterSpawnsCurrent(2, oE7); PLogI("set.current", GetEncounterSpawnsCurrent(oE7));
    SetEncounterActive(TRUE, oE7); PLogI("set.active", GetEncounterActive(oE7));
    SetEncounterActive(5, oE7); PLogI("set.active5", GetEncounterActive(oE7));
    SetEncounterActive(FALSE, oE7);
    PLogI("isenc.walker", GetIsEncounterCreature(o5));
    PLogI("enc.type", GetObjectType(oE7));
    PLog("enc.resref", GetResRef(oE7));
    PLog("enc.name", GetName(oE7));
    Walker("E6", STANDARD_FACTION_COMMONER, FALSE);
    Walker("E7", STANDARD_FACTION_COMMONER, FALSE);
    DelayCommand(12.0, Report("t12"));
    DelayCommand(13.0, KillE1());
    DelayCommand(20.0, Report("t20"));
    DelayCommand(30.0, Report("t30"));
    DelayCommand(31.0, PDone());
}
