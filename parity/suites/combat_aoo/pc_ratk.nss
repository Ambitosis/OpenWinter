// Runner OnPhysicalAttacked: counts attacks of opportunity (and any other attacks) on the runner.
#include "pcombat_inc"
void main() { PAdd(GetLocalString(OBJECT_SELF, "R") + ".hits.on.runner"); }
