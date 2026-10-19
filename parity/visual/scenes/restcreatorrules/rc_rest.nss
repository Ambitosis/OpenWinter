#include "rc_inc"
void main()
{
    object pc=GetLastPCRested();int kind=GetLastRestEventType();RCBoth(pc,"module_hook_"+IntToString(kind));
    if(kind==REST_EVENTTYPE_REST_STARTED&&GetLocalInt(pc,"veto"))AssignCommand(pc,ClearAllActions(TRUE));
}
