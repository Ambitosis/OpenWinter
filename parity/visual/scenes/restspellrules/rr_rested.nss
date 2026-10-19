#include "rr_inc"
void main()
{
    SetLocalInt(OBJECT_SELF,"rested",GetLocalInt(OBJECT_SELF,"rested")+1);
    RRState(OBJECT_SELF,"creature_rested_hook");
}
