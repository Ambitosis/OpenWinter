#include "parity_inc"
#include "parity_time"
void main()
{
    object a=GetLastSpellCaster();
    PLogI("event_spell",GetLastSpell());
    PLogI("event_caster",a==GetLocalObject(OBJECT_SELF,"caster"));
    PLogF("event_time",PNow()-GetLocalFloat(a,"start"),0.25);
    if(GetLastSpell()==601 && a==GetLocalObject(OBJECT_SELF,"caster"))SetLocalInt(OBJECT_SELF,"events",GetLocalInt(OBJECT_SELF,"events")+1);
}
