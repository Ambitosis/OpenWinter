#include "cl_inc"
void main()
{
    object c=OBJECT_SELF;SetLocalInt(c,"impacts",GetLocalInt(c,"impacts")+1);
    CLNote("impact="+GetTag(c)+"|spell="+IntToString(GetSpellId())
        +"|caster_matches="+IntToString(GetSpellCastItem()==OBJECT_INVALID&&GetLocalObject(c,"expected")==c)
        +"|target_matches="+IntToString(GetDistanceBetweenLocations(GetSpellTargetLocation(),GetLocalLocation(c,"target"))<0.001)
        +"|elapsed="+FloatToString(CLNow()-GetLocalFloat(c,"start"),0,3));
    State(c,"impact");
}
