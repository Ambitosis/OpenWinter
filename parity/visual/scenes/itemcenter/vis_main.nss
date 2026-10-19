#include "visual_inc"
void Setup(object pc)
{
    object sword=CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",Location(GetArea(pc),Vector(23.0,15.0,0.0),0.0));
    SetName(sword,"Center Sword");
    object armor=CreateObject(OBJECT_TYPE_ITEM,"nw_aarcl001",Location(GetArea(pc),Vector(17.0,15.0,0.0),0.0));
    SetName(armor,"Center Armor");
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    DelayCommand(.5,Setup(pc));DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(20.0,VDone());
}
