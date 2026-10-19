// Target OnPhysicalAttacked: when the first close and near archers shoot (module time).
#include "parity_inc"
void main()
{
    object a = GetLastAttacker();
    object m = GetModule();
    if (a != GetLocalObject(m, "close0") && a != GetLocalObject(m, "near0")) return;
    int nMin = GetTimeMinute(), nSec = GetTimeSecond(), nMs = GetTimeMillisecond();
    SetLocalString(m, "SHOTS", GetLocalString(m, "SHOTS") + GetLocalString(a, "G") + "@" + IntToString(nMin * 60 + nSec) + "." + IntToString(nMs) + " ");
}
