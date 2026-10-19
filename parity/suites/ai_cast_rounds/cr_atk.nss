#include "parity_inc"
#include "parity_time"
void main()
{
    object o = OBJECT_SELF;
    SetLocalString(o, "CR_ATK", GetLocalString(o, "CR_ATK") + "x@" + FloatToString(PSince() - GetLocalFloat(o, "CR_T0"), 0, 1) + " ");
}
