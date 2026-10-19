// Target OnPhysicalAttacked: the hit points the target has when the blow is announced.
#include "pcombat_inc"
void main()
{
    object a = GetLastAttacker();
    PAdd(GetLocalString(a, "G") + ".announced");
    PAdd(GetLocalString(a, "G") + ".hpseen", GetCurrentHitPoints());
    PcOnAttacked();
}
