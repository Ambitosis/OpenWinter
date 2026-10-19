#include "eb_inc"
void main() { Log("event|exit|" + GetTag(OBJECT_SELF) + "|" + GetName(GetExitingObject()) + "|cur=" + IntToString(GetEncounterSpawnsCurrent()) + "|active=" + IntToString(GetEncounterActive())); }
