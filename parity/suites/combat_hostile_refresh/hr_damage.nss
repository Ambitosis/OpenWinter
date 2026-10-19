#include "hr_inc"
void main()
{
    object t=OBJECT_SELF;object a=GetLocalObject(t,"caster");
    int count=GetLocalInt(t,"damage_events")+1;SetLocalInt(t,"damage_events",count);
    string stage="damage"+IntToString(count);string k=GetTag(a)+"."+stage;
    PLogI(k+".caster",GetLastDamager()==a);PLogI(k+".amount",GetTotalDamageDealt());HState(a,t,stage);
}
