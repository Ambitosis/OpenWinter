#include "rd_inc"
void main()
{
    SetLocalInt(OBJECT_SELF,"spell_events",GetLocalInt(OBJECT_SELF,"spell_events")+1);
    SetLocalInt(OBJECT_SELF,"last_spell",GetLastSpell());
    RDState(OBJECT_SELF,"stock_spell_callback");
}
