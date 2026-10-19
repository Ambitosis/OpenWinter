#include "parity_inc"
#include "parity_time"
void main()
{
    object a = GetLastAttacker();
    string k = "TU_ATK";
    SetLocalString(a, k, GetLocalString(a, k) + IntToString(GetLastAttackType(a)) + "@" + FloatToString(PSince() - GetLocalFloat(a, "TU_T0"), 0, 1) + " ");
}
