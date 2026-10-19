// Target OnPhysicalAttacked: a swing ("s<n>"), seconds since PStart.
#include "parity_inc"
#include "parity_time"
void main(){object a=GetLastAttacker();int n=GetLocalInt(a,"sn");SetLocalFloat(a,"s"+IntToString(n),PSince());SetLocalInt(a,"sn",n+1);}
