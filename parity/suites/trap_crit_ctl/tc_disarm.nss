// OnDisarm: the trap was disarmed.
#include "parity_inc"
#include "parity_time"
void main(){object m=GetModule();string k="EV_"+GetTag(OBJECT_SELF);SetLocalString(m,k,GetLocalString(m,k)+"disarm@"+FloatToString(PSince(),0,2)+" ");}
