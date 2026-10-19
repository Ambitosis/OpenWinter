#include "ea_inc"
void main()
{
    object c=GetPCItemLastUnequippedBy();
    SetLocalInt(c,"unequip_events",GetLocalInt(c,"unequip_events")+1);
    EAState(c,"unequip_event");
}
