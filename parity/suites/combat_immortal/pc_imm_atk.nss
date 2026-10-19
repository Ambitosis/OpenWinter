// OnPhysicalAttacked: counts the attack on this creature, then runs the script it had (the stock one or none).
#include "pcombat_inc"
void main()
{
    PAdd(GetLocalString(OBJECT_SELF, "C") + "." + GetLocalString(OBJECT_SELF, "S") + ".attacked");
    string s = GetLocalString(OBJECT_SELF, "ATK");
    if (s != "") ExecuteScript(s, OBJECT_SELF);
}
