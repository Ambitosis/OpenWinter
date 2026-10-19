#include "rp_inc"
void main()
{
    object pc=GetLastPCRested();int kind=GetLastRestEventType();
    RPState(pc,"module_hook_"+IntToString(kind));
}
