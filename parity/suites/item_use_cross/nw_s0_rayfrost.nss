#include "parity_inc"
#include "parity_time"
void main(){object c=OBJECT_SELF;string k=GetTag(c);int n=GetLocalInt(c,"impacts")+1;SetLocalInt(c,"impacts",n);k+=".impact"+IntToString(n);PLogF(k,PNow()-GetLocalFloat(c,"start"),.15);PLogI(k+".spell",GetSpellId());PLogI(k+".itemValid",GetIsObjectValid(GetSpellCastItem()));PLogI(k+".stack",GetItemStackSize(GetSpellCastItem()));PLogI(k+".charges",GetItemCharges(GetSpellCastItem()));}
