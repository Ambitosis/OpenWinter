#include "pa_inc"
void main()
{
    SetLocalInt(OBJECT_SELF,"impacts",GetLocalInt(OBJECT_SELF,"impacts")+1);
    vector at=GetPosition(OBJECT_SELF);vector to=GetPositionFromLocation(GetSpellTargetLocation());
    PANote("impact="+GetTag(OBJECT_SELF)+"|elapsed="+FloatToString(PANow()-GetLocalFloat(OBJECT_SELF,"start"),0,3)
        +"|spell="+IntToString(GetSpellId())+"|x="+FloatToString(at.x,0,3)+"|y="+FloatToString(at.y,0,3)
        +"|tx="+FloatToString(to.x,0,3)+"|ty="+FloatToString(to.y,0,3));
}
