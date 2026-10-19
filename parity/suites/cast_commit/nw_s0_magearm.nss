#include "parity_inc"
#include "parity_time"
void main()
{
 SetLocalInt(OBJECT_SELF,"impacts",GetLocalInt(OBJECT_SELF,"impacts")+1);
 SetLocalFloat(OBJECT_SELF,"impactTime",PNow()-GetLocalFloat(OBJECT_SELF,"start"));
}
