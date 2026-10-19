#include "parity_inc"
#include "parity_time"
void main(){object c=OBJECT_SELF;object it=GetSpellCastItem();int n=GetLocalInt(c,"impacts")+1;SetLocalInt(c,"impacts",n);string k=GetTag(c)+".impact"+IntToString(n);PLogF(k,PNow()-GetLocalFloat(c,"start"),.15);PLogI(k+".item",GetIsObjectValid(it));PLogI(k+".stack",GetItemStackSize(it));PLogI(k+".charges",GetItemCharges(it));PLogI(k+".self",GetSpellTargetObject()==c);}
