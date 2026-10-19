#include "ew_inc"
void main()
{
    Log("exhausted|" + GetTag(OBJECT_SELF) + "|cur=" + IntToString(GetEncounterSpawnsCurrent()) + "|active=" + IntToString(GetEncounterActive()));
}
