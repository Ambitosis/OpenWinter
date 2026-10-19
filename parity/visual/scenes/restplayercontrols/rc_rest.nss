#include "rc_inc"
void main()
{
    object pc=GetLastPCRested();int kind=GetLastRestEventType();
    RCState(pc,"module_hook_"+IntToString(kind));
    if(kind==REST_EVENTTYPE_REST_STARTED&&GetLocalInt(pc,"trial")==3)
    {AssignCommand(pc,ClearAllActions(TRUE));RCState(pc,"hook_after_clear");}
}
