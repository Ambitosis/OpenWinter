#include "ep_inc"
void main() { EpEv(OBJECT_SELF, "enter|" + EpWho(GetEnteringObject()) + "|cur=" + IntToString(GetEncounterSpawnsCurrent()) + "|active=" + IntToString(GetEncounterActive())); }
