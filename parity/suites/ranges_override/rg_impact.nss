#include "parity_inc"
void main(){string k=GetLocalString(OBJECT_SELF,"key");SetLocalInt(OBJECT_SELF,"executed",1);PLogF(k+".distance",GetDistanceBetweenLocations(GetLocation(OBJECT_SELF),GetSpellTargetLocation()),0.5);PLogF(k+".moved",GetDistanceBetweenLocations(GetLocation(OBJECT_SELF),GetLocalLocation(OBJECT_SELF,"start")),0.5);}
