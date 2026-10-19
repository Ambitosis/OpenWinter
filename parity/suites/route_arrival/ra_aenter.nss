// Area OnEnter: when a walker appears in each area (seconds since PStart).
#include "parity_inc"
#include "parity_time"
void main()
{
    object o=GetEnteringObject();
    if(GetLocalInt(o,"RA"))SetLocalString(o,"ENTER",GetLocalString(o,"ENTER")+GetTag(OBJECT_SELF)+"@"+FloatToString(PSince(),0,2)+" ");
}
