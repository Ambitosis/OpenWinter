#include "hs_inc"
void main()
{
    object a=GetLocalObject(OBJECT_SELF,"caster");string k=GetTag(a);
    PLogI(k+".damage_caster",GetLastDamager()==a);PLogI(k+".damage_amount",GetTotalDamageDealt());
    SetLocalInt(OBJECT_SELF,"damage_events",GetLocalInt(OBJECT_SELF,"damage_events")+1);
    HState(a,OBJECT_SELF,"damage_callback");
}
