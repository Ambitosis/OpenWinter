// Target OnDeath: which of the attacker's attacks this round killed it.
#include "pcombat_inc"
void main()
{
    object a = GetLastKiller();
    PAdd(GetLocalString(OBJECT_SELF, "G") + ".died");
    SetLocalInt(a, "KILLIDX", GetLocalInt(a, "IDX"));
}
