#include "ep_inc"
void main() { EpEv(OBJECT_SELF, "user|" + IntToString(GetUserDefinedEventNumber())); }
