#include "ep_inc"
void main() { EpEv(OBJECT_SELF, "exit|" + EpWho(GetExitingObject()) + "|cur=" + IntToString(GetEncounterSpawnsCurrent())); }
