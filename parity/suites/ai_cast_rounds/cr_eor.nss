#include "parity_inc"
#include "parity_time"
void main()
{
    object o = OBJECT_SELF;
    SetLocalString(o, "CR_EOR", GetLocalString(o, "CR_EOR") + "x@" + FloatToString(PSince() - GetLocalFloat(o, "CR_T0"), 0, 1) + " ");
}
