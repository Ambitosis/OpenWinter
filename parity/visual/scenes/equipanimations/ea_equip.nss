#include "ea_inc"
void main()
{
    object c=GetPCItemLastEquippedBy();
    SetLocalInt(c,"equip_events",GetLocalInt(c,"equip_events")+1);
    EAState(c,"equip_event");
}
