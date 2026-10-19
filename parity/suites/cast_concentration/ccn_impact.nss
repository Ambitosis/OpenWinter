#include "parity_inc"
#include "parity_time"
void main(){object o=OBJECT_SELF;SetLocalInt(o,"impacts",GetLocalInt(o,"impacts")+1);SetLocalFloat(o,"impactAt",PNow()-GetLocalFloat(o,"start"));}
