#include "er_inc"
void main() { Log("event|exhausted|" + GetTag(OBJECT_SELF) + "|" + EncState(OBJECT_SELF)); }
