#include "es_inc"
void main() { Log("event|exit|" + GetTag(OBJECT_SELF) + "|" + GetName(GetExitingObject()) + "|" + EncState(OBJECT_SELF)); }
