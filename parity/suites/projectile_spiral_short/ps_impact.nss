#include "parity_inc"
#include "parity_time"
void main()
{
    string k=GetLocalString(OBJECT_SELF,"case");
    SetLocalInt(OBJECT_SELF,"impacts",GetLocalInt(OBJECT_SELF,"impacts")+1);
    PLogF(k+".impact",PNow()-GetLocalFloat(OBJECT_SELF,"start"),0.10);
    PLogI(k+".spell",GetSpellId());
    PLogI(k+".stationary",GetDistanceBetweenLocations(GetLocation(OBJECT_SELF),GetLocalLocation(OBJECT_SELF,"origin"))<0.001);
    PLogF(k+".impact_distance",GetDistanceBetweenLocations(GetLocation(OBJECT_SELF),GetSpellTargetLocation()),0.001);
    PLogI(k+".target_matches",GetDistanceBetweenLocations(GetSpellTargetLocation(),GetLocalLocation(OBJECT_SELF,"target"))<0.001);
}
