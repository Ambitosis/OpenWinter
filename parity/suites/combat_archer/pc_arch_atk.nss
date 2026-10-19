// Archer OnPhysicalAttacked: attacks on the archer (the target fighting back, and any attacks of opportunity).
#include "pcombat_inc"
void main() { PAdd(GetLocalString(OBJECT_SELF, "G") + ".onarcher"); }
