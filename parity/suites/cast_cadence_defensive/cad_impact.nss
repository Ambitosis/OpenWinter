#include "parity_inc"
#include "parity_time"
void main(){object o=OBJECT_SELF;int n=GetLocalInt(o,"impacts");string k=GetTag(o)+".impact"+IntToString(n);PLogF(k+".time",PNow()-GetLocalFloat(o,"start"),0.2);PLogI(k+".combat",GetIsInCombat(o));PLogI(k+".meta",GetMetaMagicFeat());SetLocalInt(o,"impacts",n+1);}
