#include "parity_inc"
#include "parity_time"
void main(){PLogF("impact."+GetTag(OBJECT_SELF),PNow()-GetLocalFloat(OBJECT_SELF,"start"),0.15);}
