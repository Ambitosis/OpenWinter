#include "parity_inc"
#include "parity_time"
// Logger in place of the stock impact script: OBJECT_SELF is the caster.
void main()
{
    object c=OBJECT_SELF;string k=GetLocalString(c,"case");int n=GetLocalInt(c,"impacts")+1;SetLocalInt(c,"impacts",n);
    if(n>1)return;
    PLogF(k+".impact",PNow()-GetLocalFloat(c,"start"),.15);
    PLogF(k+".moved",GetDistanceBetweenLocations(GetLocation(c),GetLocalLocation(c,"from")),.15);
    PLogF(k+".gap",GetDistanceBetween(c,GetLocalObject(c,"target")),.15);
    PLogI(k+".spell",GetSpellId());
}
