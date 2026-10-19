#include "visual_inc"
void Report(object pc,string phase)
{
    object item=GetLocalObject(pc,"item"),held=GetLocalObject(pc,"held");
    vector v=GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|"+phase+"|item="+IntToString(GetIsObjectValid(item))
        +"|owned="+IntToString(GetItemPossessor(item)==pc)
        +"|groundRight="+IntToString(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc)==item)
        +"|groundLeft="+IntToString(GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc)==item)
        +"|heldRight="+IntToString(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc)==held)
        +"|heldLeft="+IntToString(GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc)==held)
        +"|action="+IntToString(GetCurrentAction(pc))+"|x="+FloatToString(v.x)+"|y="+FloatToString(v.y));
}
void EquipHeld(object pc){AssignCommand(pc,ActionEquipItem(GetLocalObject(pc,"held"),INVENTORY_SLOT_RIGHTHAND));}
void Prepare(object pc,string phase)
{
    AssignCommand(pc,ClearAllActions(TRUE));
    DestroyObject(GetLocalObject(pc,"item"));DestroyObject(GetLocalObject(pc,"held"));
    DestroyObject(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc));DestroyObject(GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc));
    VMovePC(pc,20.0,15.0,0.0,90.0);
    object held=CreateItemOnObject("nw_wswdg001",pc);SetLocalObject(pc,"held",held);
    DelayCommand(.2,EquipHeld(pc));
    object item=CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",Location(GetArea(pc),Vector(23.0,15.0,0.0),0.0));
    SetName(item,"Ground "+phase);SetLocalObject(pc,"item",item);
}
void Ring(float t,string phase,int key)
{
    DelayCommand(t,WriteTimestampedLogEntry("VISUAL|hover|0.754,0.66"));
    DelayCommand(t+.3,WriteTimestampedLogEntry("VISUAL|mouse|0.754,0.66|down|right"));
    DelayCommand(t+1.0,VShot(phase+"_root"));
    DelayCommand(t+1.5,WriteTimestampedLogEntry("VISUAL|hover|0.832,0.66"));
    DelayCommand(t+2.5,VShot(phase+"_east"));
    DelayCommand(t+3.0,WriteTimestampedLogEntry("VISUAL|hover|0.676,0.66"));
    DelayCommand(t+4.0,VShot(phase+"_west"));
    DelayCommand(t+4.5,WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|tap"));
    DelayCommand(t+7.5,WriteTimestampedLogEntry("VISUAL|mouse|0.754,0.66|up|right"));
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_ACQUIRE_ITEM,"vis_acq");
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_EQUIP_ITEM,"vis_equip");
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
    WriteTimestampedLogEntry("VISUAL|note|profile|race="+IntToString(GetRacialType(pc))+"|wizard="+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc)));
    DelayCommand(.5,Prepare(pc,"east"));DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(3.0,Report(pc,"east_before"));Ring(4.0,"east",102);
    DelayCommand(8.6,Report(pc,"east_0.1"));DelayCommand(9.2,Report(pc,"east_0.7"));
    DelayCommand(10.0,Report(pc,"east_1.5"));DelayCommand(12.0,Report(pc,"east_3.5"));
    DelayCommand(13.0,VShot("east_after"));
    DelayCommand(15.0,Prepare(pc,"west"));DelayCommand(17.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(18.0,Report(pc,"west_before"));Ring(19.0,"west",100);
    DelayCommand(23.6,Report(pc,"west_0.1"));DelayCommand(24.2,Report(pc,"west_0.7"));
    DelayCommand(25.0,Report(pc,"west_1.5"));DelayCommand(27.0,Report(pc,"west_3.5"));
    DelayCommand(28.0,VShot("west_after"));DelayCommand(29.0,VDone());
}
