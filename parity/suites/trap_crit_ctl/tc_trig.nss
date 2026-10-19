// OnTrapTriggered: the trap went off (kept on the module by the trap's tag: a one-shot trap trigger is destroyed when it goes off).
#include "parity_inc"
#include "parity_time"
void main(){object m=GetModule();string k="EV_"+GetTag(OBJECT_SELF);SetLocalString(m,k,GetLocalString(m,k)+"trig@"+FloatToString(PSince(),0,2)+" ");}
