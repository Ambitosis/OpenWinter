#include "rr_inc"
void main()
{
    object pc=GetLastPCRested();int kind=GetLastRestEventType();
    RRState(pc,"module_hook_"+IntToString(kind));
    if(kind==REST_EVENTTYPE_REST_STARTED&&GetLocalInt(pc,"trial")==1)
    {AssignCommand(pc,ClearAllActions(TRUE));RRState(pc,"hook_after_clear");}
}
