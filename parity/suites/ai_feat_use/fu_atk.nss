#include "parity_inc"
#include "parity_time"
void main()
{
    object t = OBJECT_SELF;
    object a = GetLastAttacker();
    string s = IntToString(GetLastAttackType(a)) + "/" + IntToString(GetLastAttackMode(a)) + "@" + FloatToString(PSince() - GetLocalFloat(a, "FU_T0"), 0, 1);
    SetLocalString(a, "FU_ATK", GetLocalString(a, "FU_ATK") + s + " ");
}
