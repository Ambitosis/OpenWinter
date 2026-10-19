#include "dy_inc"
// OnPlayerDeath: only records the death; the scene raises her itself (no death panel).
void main()
{
    object pc=GetLastPlayerDied();object m=GetModule();int n=GetLocalInt(m,"deaths")+1;SetLocalInt(m,"deaths",n);
    DyNote(pc,"ev_death_"+IntToString(n));
}
