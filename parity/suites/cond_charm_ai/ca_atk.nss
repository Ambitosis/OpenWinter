// OnPhysicalAttacked on a bystander.
#include "parity_time"
void main()
{
    object m = GetModule();
    object c = GetLastAttacker();
    string k = GetLocalString(c, "G");
    if (GetLocalInt(OBJECT_SELF, "hit" + k)) return;
    SetLocalInt(OBJECT_SELF, "hit" + k, 1);
    SetLocalString(m, k + ".log", GetLocalString(m, k + ".log") + " hit" + GetLocalString(OBJECT_SELF, "R") + "@" + FloatToString(PNow() - GetLocalFloat(c, "T0"), 0, 1));
}
