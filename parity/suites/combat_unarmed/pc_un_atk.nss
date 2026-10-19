// Attacker OnPhysicalAttacked: the passive target attacked it (an attack of opportunity).
#include "pcombat_inc"
void main() { PAdd(GetLocalString(OBJECT_SELF, "G") + ".back.atk"); }
