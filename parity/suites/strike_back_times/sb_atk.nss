// Caster OnPhysicalAttacked: its victim's attacks, logged with times.
#include "parity_inc"
#include "parity_time"
void main()
{
    object o = OBJECT_SELF;
    SetLocalString(o, "SEQ", GetLocalString(o, "SEQ") + "V@" + FloatToString(PSince(), 0, 2) + " ");
}
