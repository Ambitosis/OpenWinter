// OnClick (trigger or door): who clicked, when, and how far it stood from the object's position.
#include "parity_inc"
#include "parity_time"
void main()
{
    object c=GetClickingObject();object o=OBJECT_SELF;
    SetLocalString(o,"CLICK",GetLocalString(o,"CLICK")+(GetIsObjectValid(c)?GetTag(c):"invalid")+"@"+FloatToString(PSince(),0,2)+" ");
    SetLocalInt(c,"CLICKED_"+GetTag(o),GetLocalInt(c,"CLICKED_"+GetTag(o))+1);
}
