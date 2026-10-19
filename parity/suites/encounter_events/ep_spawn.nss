#include "ep_inc"
void main()
{
    object oEnc = GetNearestObject(OBJECT_TYPE_ENCOUNTER);
    vector v = GetPosition(OBJECT_SELF), c = GetPosition(oEnc);
    EpEv(oEnc, "spawn|" + GetTag(OBJECT_SELF) + "|dx=" + EpF(v.x - c.x) + "|dy=" + EpF(v.y - c.y)
        + "|face=" + EpF(GetFacing(OBJECT_SELF)) + "|isenc=" + IntToString(GetIsEncounterCreature())
        + "|hostileRep=" + IntToString(GetStandardFactionReputation(STANDARD_FACTION_HOSTILE, OBJECT_SELF))
        + "|commonerRep=" + IntToString(GetStandardFactionReputation(STANDARD_FACTION_COMMONER, OBJECT_SELF))
        + "|cur=" + IntToString(GetEncounterSpawnsCurrent(oEnc)) + "|ai=" + IntToString(GetAILevel()));
}
