// Caster OnPhysicalAttacked: attacks it takes from its victim, and when the first came.
#include "parity_inc"
#include "parity_time"
void main()
{
    object o = OBJECT_SELF;
    string g = GetLocalString(o, "G");
    object m = GetModule();
    SetLocalInt(m, g + ".hits", GetLocalInt(m, g + ".hits") + 1);
    if (GetLocalFloat(o, "FIRST") == 0.0) SetLocalFloat(o, "FIRST", PSince());
}
