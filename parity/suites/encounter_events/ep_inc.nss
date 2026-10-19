#include "parity_inc"

object EpArea() { return GetObjectByTag("PARITY_AREA"); }
object EpEnc(string sTag) { return GetObjectByTag(sTag); }

// Per-encounter event sequence: "<enc>.ev.<n>" = what happened.
void EpEv(object oEnc, string sWhat)
{
    int n = GetLocalInt(oEnc, "ev") + 1;
    SetLocalInt(oEnc, "ev", n);
    PLog(GetTag(oEnc) + ".ev." + IntToString(n), sWhat);
}

string EpWho(object o)
{
    if (!GetIsObjectValid(o)) return "invalid";
    return GetTag(o);
}

string EpF(float f) { return FloatToString(f, 0, 1); }

void EpReport(object oEnc)
{
    string t = GetTag(oEnc);
    PLogI(t + ".active", GetEncounterActive(oEnc));
    PLogI(t + ".max", GetEncounterSpawnsMax(oEnc));
    PLogI(t + ".current", GetEncounterSpawnsCurrent(oEnc));
    PLogI(t + ".difficulty", GetEncounterDifficulty(oEnc));
    PLogI(t + ".beats", GetLocalInt(oEnc, "hb") > 0);
}

// Encounter creatures whose nearest encounter is oEnc.
void EpCount(object oEnc, string sLabel)
{
    int nAlive = 0, nAll = 0;
    string sList = "";
    object o = GetFirstObjectInArea(EpArea());
    while (GetIsObjectValid(o))
    {
        if (GetObjectType(o) == OBJECT_TYPE_CREATURE && GetIsEncounterCreature(o)
            && GetNearestObject(OBJECT_TYPE_ENCOUNTER, o) == oEnc)
        {
            nAll++;
            if (!GetIsDead(o)) nAlive++;
            sList += GetTag(o) + ",";
        }
        o = GetNextObjectInArea(EpArea());
    }
    PLogI(GetTag(oEnc) + "." + sLabel + ".count", nAll);
    PLogI(GetTag(oEnc) + "." + sLabel + ".alive", nAlive);
    PLog(GetTag(oEnc) + "." + sLabel + ".list", sList);
}
