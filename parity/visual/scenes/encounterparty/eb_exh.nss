#include "eb_inc"
void main()
{
    if (GetStringLeft(GetLocalString(Mod(), "cur"), 1) == "k")
        Log("event|exhausted|" + GetTag(OBJECT_SELF) + "|cur=" + IntToString(GetEncounterSpawnsCurrent()) + "|active=" + IntToString(GetEncounterActive()));
}
