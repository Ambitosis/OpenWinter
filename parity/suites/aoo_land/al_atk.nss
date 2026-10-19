// Caster OnPhysicalAttacked: who swung at it and when (seconds since the suite started).
#include "parity_inc"
#include "parity_time"
void main(){object c=OBJECT_SELF;SetLocalString(c,"atk",GetLocalString(c,"atk")+FloatToString(PSince(),0,2)+" ");}
