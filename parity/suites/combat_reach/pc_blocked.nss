// Attacker OnBlocked: counts bumps while closing in.
#include "pcombat_inc"
void main() { PAdd(GetLocalString(OBJECT_SELF, "G") + ".blocked"); }
