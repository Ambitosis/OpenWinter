#include "parity_inc"
#include "parity_time"
void main()
{
    object caster=GetLastSpellCaster();
    PLogI("event_spell",GetLastSpell());
    PLogI("event_caster",caster==GetLocalObject(OBJECT_SELF,"caster"));
    PLogF("event_time",PNow()-GetLocalFloat(caster,"start"),0.2);
}
