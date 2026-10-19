#include "parity_inc"
#include "parity_time"
void main(){object c=OBJECT_SELF;SetLocalString(c,"eor",GetLocalString(c,"eor")+FloatToString(PNow()-GetLocalFloat(c,"start"),0,2)+" ");}
