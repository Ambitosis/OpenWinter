// Target OnDamaged: the blow of the attacker's latest swing ("d<swing index>").
#include "parity_inc"
#include "parity_time"
void main(){object a=GetLastDamager();int n=GetLocalInt(a,"sn")-1;if(n>=0&&GetLocalFloat(a,"d"+IntToString(n))==0.0)SetLocalFloat(a,"d"+IntToString(n),PSince());}
