#include "parity_inc"
#include "parity_time"
void main(){object c=OBJECT_SELF;SetLocalString(c,"impacts",GetLocalString(c,"impacts")+FloatToString(PNow()-GetLocalFloat(c,"start"),0,3)+" ");}
