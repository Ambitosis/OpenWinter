// Target OnDeath: how long after the killing attack it died.
#include "pcombat_inc"
void main()
{
    int at = GetLocalInt(OBJECT_SELF, "ATKAT");
    if (at <= 0) return;
    string g = GetLocalString(OBJECT_SELF, "G");
    PAdd(g + ".die", PClock() - at);
    PAdd(g + ".dien");
}
