#include "parity_inc"
#include "parity_time"
void main(){object c=GetLocalObject(OBJECT_SELF,"caster");int n=GetLocalInt(c,"swings");if(n==0)PLogF(GetTag(c)+".firstSwing",PNow()-GetLocalFloat(c,"start"),0.25);SetLocalInt(c,"swings",n+1);}
