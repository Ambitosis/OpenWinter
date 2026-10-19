// Target OnDamaged: how long after the attack the damage came.
#include "pcombat_inc"
void main()
{
    int at = GetLocalInt(OBJECT_SELF, "ATKAT");
    if (at <= 0) return;
    string g = GetLocalString(OBJECT_SELF, "G");
    PAdd(g + ".lag", PClock() - at);
    PAdd(g + ".lagn");
}
