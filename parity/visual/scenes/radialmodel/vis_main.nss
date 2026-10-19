#include "visual_inc"
void Setup(object pc)
{
    object item=CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",Location(GetArea(pc),Vector(23.0,15.0,0.0),0.0));
    SetName(item,"Model Center Probe");SetLocalObject(pc,"item",item);
    WriteTimestampedLogEntry("VISUAL|note|profile|pc="+IntToString(GetIsPC(pc))+"|base="+IntToString(GetBaseItemType(item))+"|model="+IntToString(GetItemAppearance(item,ITEM_APPR_TYPE_WEAPON_MODEL,ITEM_APPR_WEAPON_MODEL_TOP)));
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    DelayCommand(.5,Setup(pc));DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(3.5,WriteTimestampedLogEntry("VISUAL|hover|0.754,0.66"));
    DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|mouse|0.754,0.66|down|right"));
    DelayCommand(5.0,VShot("center_1"));DelayCommand(5.5,VShot("center_15"));
    DelayCommand(6.0,VShot("center_2"));DelayCommand(7.0,VShot("center_3"));
    DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|hover|0.832,0.66"));
    DelayCommand(9.0,VShot("east"));
    DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|hover|0.754,0.66"));
    DelayCommand(11.0,VShot("center_return"));
    DelayCommand(12.0,WriteTimestampedLogEntry("VISUAL|mouse|0.754,0.66|up|right"));
    DelayCommand(13.0,VShot("released"));
    DelayCommand(14.0,VDone());
}
