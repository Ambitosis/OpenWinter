#include "es_inc"
void main() { Log("event|enter|" + GetTag(OBJECT_SELF) + "|" + GetName(GetEnteringObject()) + "|isPC=" + IntToString(GetIsPC(GetEnteringObject())) + "|" + EncState(OBJECT_SELF)); }
