#include "parity_inc"
#include "parity_time"
void main()
{
    object a=GetLastSpellCaster();string id=GetTag(a);
    int valid=GetLastSpell()==601 && a==GetLocalObject(OBJECT_SELF,"caster");
    if(valid)SetLocalInt(OBJECT_SELF,"events",GetLocalInt(OBJECT_SELF,"events")+1);
    PLogI(id+".event_spell",GetLastSpell());PLogI(id+".event_caster",valid);
    PLogF(id+".arrival",PNow()-GetLocalFloat(a,"start"),0.2);
    PLogF(id+".arrival_distance",GetDistanceBetween(a,OBJECT_SELF),0.01);
    vector c=GetPosition(a);vector t=GetPosition(OBJECT_SELF);
    PLogF(id+".arrival_cx",c.x,0.01);PLogF(id+".arrival_cy",c.y,0.01);
    PLogF(id+".arrival_tx",t.x,0.01);PLogF(id+".arrival_ty",t.y,0.01);
}
