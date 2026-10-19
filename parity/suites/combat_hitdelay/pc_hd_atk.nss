// Target OnPhysicalAttacked: when the attack came.
#include "pcombat_inc"
void main()
{
    SetLocalInt(OBJECT_SELF, "ATKAT", PClock());
    SetLocalInt(OBJECT_SELF, "POLLED", 0);
}
