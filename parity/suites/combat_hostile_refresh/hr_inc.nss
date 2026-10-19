#include "parity_inc"
#include "parity_time"
void HState(object a,object t,string stage)
{
    string k=GetTag(a)+"."+stage;
    PLogF(k+".time",PNow()-GetLocalFloat(a,"start"),0.10);
    PLogI(k+".a_combat",GetIsInCombat(a));PLogI(k+".t_combat",GetIsInCombat(t));
    PLogI(k+".a_action",GetCurrentAction(a));PLogI(k+".t_action",GetCurrentAction(t));
    PLogI(k+".spells",GetLocalInt(t,"spells"));PLogI(k+".damage_events",GetLocalInt(t,"damage_events"));
    PLogI(k+".hp_delta",GetLocalInt(t,"hp")-GetCurrentHitPoints(t));
    PLogI(k+".stationary",GetDistanceBetweenLocations(GetLocation(a),GetLocalLocation(a,"origin"))<0.001&&GetDistanceBetweenLocations(GetLocation(t),GetLocalLocation(t,"origin"))<0.001);
}
