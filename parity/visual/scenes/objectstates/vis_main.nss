#include "visual_inc"
void main()
{
    object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
    VBegin(pc); VHidePC(pc);
    object a=CreateObject(OBJECT_TYPE_PLACEABLE,"plc_armoire",Location(GetArea(pc),Vector(20.0,21.0,0.0),180.0));
    DelayCommand(1.0,VCamera(pc,180.0,6.0,65.0));
    DelayCommand(3.0,VShot("closed"));
    DelayCommand(4.0,AssignCommand(a,PlayAnimation(ANIMATION_PLACEABLE_OPEN)));
    DelayCommand(6.0,VShot("script_open"));
    DelayCommand(8.0,AssignCommand(a,PlayAnimation(ANIMATION_PLACEABLE_CLOSE)));
    DelayCommand(10.0,VShot("script_close"));
    DelayCommand(12.0,VDone());
}
