#include "dy_inc"
// OnPlayerDying: only records the event, so the player stays dying (the stock nw_o0_dying would kill).
void main()
{
    object pc=GetLastPlayerDying();object m=GetModule();int n=GetLocalInt(m,"dyings")+1;SetLocalInt(m,"dyings",n);
    DyNote(pc,"ev_dying_"+IntToString(n)+"|valid="+IntToString(GetIsObjectValid(pc)));
}
