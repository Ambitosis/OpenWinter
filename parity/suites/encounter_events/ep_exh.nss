#include "ep_inc"
void main() { EpEv(OBJECT_SELF, "exhausted|cur=" + IntToString(GetEncounterSpawnsCurrent()) + "|active=" + IntToString(GetEncounterActive())); }
