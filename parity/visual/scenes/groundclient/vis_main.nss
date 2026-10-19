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
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_ACQUIRE_ITEM,"vis_acq");
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_EQUIP_ITEM,"vis_equip");
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
    DelayCommand(.5,Prepare(pc,"main"));DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(14.0,Prepare(pc,"offhand"));DelayCommand(15.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(32.0,VDone());
}
