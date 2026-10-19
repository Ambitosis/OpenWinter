// Authored observer: measure engine lifecycle, not Turn Undead's stock statistics.
#include "parity_inc"
#include "parity_time"
void main(){int n=GetLocalInt(OBJECT_SELF,"impacts")+1;SetLocalInt(OBJECT_SELF,"impacts",n);string k=GetTag(OBJECT_SELF)+".impact"+IntToString(n);PLogF(k,PNow()-GetLocalFloat(OBJECT_SELF,"start"),.2);PLogI(k+".uses",GetFeatRemainingUses(FEAT_TURN_UNDEAD,OBJECT_SELF));PLogI(k+".spell",GetSpellId());}
