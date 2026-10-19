// Neighbour OnPhysicalAttacked: counts the attack for the attacker's group and this neighbour's role.
#include "pcond_inc"
void main()
{
    object a = GetLastAttacker();
    CAdd(GetLocalString(a, "G") + ".atk." + GetLocalString(OBJECT_SELF, "R"));
}
