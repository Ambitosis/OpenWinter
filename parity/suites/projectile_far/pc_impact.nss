#include "parity_inc"
#include "parity_time"
void main(){string key=GetLocalString(OBJECT_SELF,"case");SetLocalInt(OBJECT_SELF,"impacts",GetLocalInt(OBJECT_SELF,"impacts")+1);float elapsed=PNow()-GetLocalFloat(OBJECT_SELF,"start");PLogF(key+".impact",elapsed,0.12);if(GetLocalInt(OBJECT_SELF,"path")==0)SetLocalFloat(OBJECT_SELF,"release",elapsed);else PLogF(key+".flight",elapsed-GetLocalFloat(OBJECT_SELF,"release"),0.08);PLogF(key+".hitDistance",GetDistanceBetweenLocations(GetLocation(OBJECT_SELF),GetSpellTargetLocation()),0.02);}
