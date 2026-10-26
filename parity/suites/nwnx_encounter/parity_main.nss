// nwnx_encounter: NWNX_Encounter readings of placed encounters (creature list, spawn points, faction, flags,
// reset time, limits, geometry), the setters read back and through the engine's own encounter functions, and Destroy.
#include "parity_inc"
#include "nwnx_encounter"

string V(vector v) { return FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2); }

void Report(string p, object e)
{
    int n = NWNX_Encounter_GetNumberOfCreaturesInEncounterList(e);
    PLogI(p + ".count", n);
    int i;
    for (i = -1; i <= n; i++)
    {
        struct NWNX_Encounter_CreatureListEntry c = NWNX_Encounter_GetEncounterCreatureByIndex(e, i);
        PLog(p + ".creature" + IntToString(i), c.resref + " cr=" + FloatToString(c.challengeRating, 0, 3) + " u=" + IntToString(c.unique)
            + " used=" + IntToString(c.alreadyUsed));
    }
    PLogI(p + ".faction", NWNX_Encounter_GetFactionId(e));
    PLogI(p + ".playeronly", NWNX_Encounter_GetPlayerTriggeredOnly(e));
    PLogI(p + ".canreset", NWNX_Encounter_GetCanReset(e));
    PLogI(p + ".resettime", NWNX_Encounter_GetResetTime(e));
    int np = NWNX_Encounter_GetNumberOfSpawnPoints(e);
    PLogI(p + ".points", np);
    for (i = -1; i <= np; i++)
    {
        location l = NWNX_Encounter_GetSpawnPointByIndex(e, i);
        PLog(p + ".point" + IntToString(i), V(GetPositionFromLocation(l)) + " f=" + FloatToString(GetFacingFromLocation(l), 0, 2)
            + " a=" + GetTag(GetAreaFromLocation(l)));
    }
    PLogI(p + ".min", NWNX_Encounter_GetMinNumSpawned(e));
    PLogI(p + ".max", NWNX_Encounter_GetMaxNumSpawned(e));
    PLogI(p + ".current", NWNX_Encounter_GetCurrentNumSpawned(e));
    PLog(p + ".geometry", NWNX_Encounter_GetGeometry(e));
}

void main()
{
    object e1 = GetObjectByTag("E1");
    object e2 = GetObjectByTag("E2");
    Report("e1", e1);
    Report("e2", e2);
    Report("inv", OBJECT_INVALID);
    Report("mod", GetModule());

    struct NWNX_Encounter_CreatureListEntry c;
    c.resref = "nw_kobold001";
    c.challengeRating = 1.5;
    c.unique = TRUE;
    c.alreadyUsed = TRUE;
    NWNX_Encounter_SetEncounterCreatureByIndex(e1, 1, c);
    NWNX_Encounter_SetEncounterCreatureByIndex(e1, 3, c);
    NWNX_Encounter_SetEncounterCreatureByIndex(e1, -1, c);
    NWNX_Encounter_SetFactionId(e1, 3);
    NWNX_Encounter_SetPlayerTriggeredOnly(e1, 4);
    NWNX_Encounter_SetCanReset(e1, FALSE);
    NWNX_Encounter_SetResetTime(e1, 123);
    NWNX_Encounter_SetGeometry(e1, "{1.0, 1.0, 0.0}{8.0, 1.0}{8.0, 6.0, 2.5}{1.0, 6.0}");
    Report("set1", e1);
    NWNX_Encounter_SetResetTime(e1, -5);
    PLogI("set1.resetneg", NWNX_Encounter_GetResetTime(e1));
    NWNX_Encounter_SetFactionId(e1, 999);
    PLogI("set1.faction999", NWNX_Encounter_GetFactionId(e1));
    NWNX_Encounter_SetGeometry(e1, "{1.0, 1.0}{2.0, 2.0}");
    PLog("set1.geometry2", NWNX_Encounter_GetGeometry(e1));
    NWNX_Encounter_SetGeometry(e1, "nonsense");
    PLog("set1.geometrybad", NWNX_Encounter_GetGeometry(e1));
    PLogI("native.max", GetEncounterSpawnsMax(e1));
    PLogI("native.current", GetEncounterSpawnsCurrent(e1));
    PLogI("native.active", GetEncounterActive(e1));
    PLogI("native.inside", GetIsInSubArea(OBJECT_INVALID, e1));

    NWNX_Encounter_Destroy(e2);
    PLogI("destroy.valid", GetIsObjectValid(e2));
    PLogI("destroy.bytag", GetIsObjectValid(GetObjectByTag("E2")));
    NWNX_Encounter_Destroy(GetModule());
    PLogI("destroy.module", GetIsObjectValid(GetModule()));
    PDone();
}
