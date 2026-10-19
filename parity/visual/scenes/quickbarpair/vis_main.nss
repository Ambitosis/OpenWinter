#include "visual_inc"
#include "qp_state"
void Key(int key){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|tap");}
void EmptyHands(object pc)
{
    AssignCommand(pc,ClearAllActions(TRUE));
    object right=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc),left=GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc);
    if(GetIsObjectValid(right))AssignCommand(pc,ActionUnequipItem(right));
    if(GetIsObjectValid(left))AssignCommand(pc,ActionUnequipItem(left));
}
void OtherHands(object pc)
{
    AssignCommand(pc,ClearAllActions(TRUE));
    object right=CreateItemOnObject("nw_wblcl001",pc,1,"QP_OTHER_RIGHT");
    object left=CreateItemOnObject("nw_ashsw001",pc,1,"QP_OTHER_LEFT");
    AssignCommand(pc,ActionEquipItem(right,INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(pc,ActionEquipItem(left,INVENTORY_SLOT_LEFTHAND));
}
void BothHands(object pc)
{
    AssignCommand(pc,ClearAllActions(TRUE));
    AssignCommand(pc,ActionEquipItem(GetLocalObject(pc,"qp_right"),INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(pc,ActionEquipItem(GetLocalObject(pc,"qp_left"),INVENTORY_SLOT_LEFTHAND));
}
void RemoveHand(object pc,int slot)
{
    object item=GetItemInSlot(slot,pc);
    if(GetIsObjectValid(item))AssignCommand(pc,ActionUnequipItem(item));
}
void MissingLeft(object pc)
{
    object carrier=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),GetPosition(pc)+Vector(1.0,0.0,0.0),0.0));
    ChangeFaction(carrier,pc);SetPlotFlag(carrier,TRUE);
    SetEventScript(carrier,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
    SetEventScript(carrier,EVENT_SCRIPT_CREATURE_ON_NOTICE,"");
    AssignCommand(carrier,ClearAllActions(TRUE));
    SetLocalObject(pc,"qp_carrier",carrier);
    AssignCommand(pc,ActionGiveItem(GetLocalObject(pc,"qp_left"),carrier));
}
void ReturnLeft(object pc)
{
    object carrier=GetLocalObject(pc,"qp_carrier");
    AssignCommand(carrier,ClearAllActions(TRUE));
    AssignCommand(carrier,ActionGiveItem(GetLocalObject(pc,"qp_left"),pc));
}
void Pulse(object pc,float t,string phase)
{
    DelayCommand(t-.1,QState(pc,phase+".before"));
    DelayCommand(t,Key(116)); // F5: the installed cleric BIC's actual paired binding, unchanged.
    DelayCommand(t+.1,QState(pc,phase+".0.1"));
    DelayCommand(t+.35,QState(pc,phase+".0.35"));
    DelayCommand(t+.7,QState(pc,phase+".0.7"));
    DelayCommand(t+1.2,QState(pc,phase+".1.2"));
    DelayCommand(t+.25,VShot(phase+"_transition"));
    DelayCommand(t+2.0,QState(pc,phase+".settled"));
    DelayCommand(t+2.1,VShot(phase+"_settled"));
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_EQUIP_ITEM,"qp_equip");
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_UNEQUIP_ITEM,"qp_unequip");
    QNote("character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
    object right=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc),left=GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc);
    SetLocalObject(pc,"qp_right",right);SetLocalObject(pc,"qp_left",left);
    QNote("fixture|cleric="+IntToString(GetLevelByClass(CLASS_TYPE_CLERIC,pc))
        +"|right="+QItem(right)+"|left="+QItem(left));
    if(GetResRef(right)!="nw_wblmml007" || GetResRef(left)!="nw_ashmsw010")
    {QNote("REJECT|installed cleric pair missing");DelayCommand(.5,VDone());return;}
    SetPlotFlag(pc,TRUE);
    DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));
    DelayCommand(2.0,VShot("a_native_pair_binding"));
    Pulse(pc,4.0,"current_pair");
    Pulse(pc,8.0,"repeat_activation");
    DelayCommand(11.0,EmptyHands(pc));
    Pulse(pc,14.0,"empty_hands");
    DelayCommand(18.0,OtherHands(pc));
    Pulse(pc,22.0,"other_equipment");
    Pulse(pc,26.0,"after_other_repeat");
    DelayCommand(29.0,BothHands(pc));
    DelayCommand(30.0,RemoveHand(pc,INVENTORY_SLOT_LEFTHAND));
    Pulse(pc,32.0,"only_main");
    DelayCommand(35.0,BothHands(pc));
    DelayCommand(36.0,RemoveHand(pc,INVENTORY_SLOT_RIGHTHAND));
    Pulse(pc,38.0,"only_offhand");
    DelayCommand(41.0,EmptyHands(pc));
    DelayCommand(42.0,MissingLeft(pc));
    Pulse(pc,46.0,"missing_offhand");
    DelayCommand(49.0,ReturnLeft(pc));
    DelayCommand(52.0,EmptyHands(pc));
    DelayCommand(54.0,QState(pc,"final_export"));
    DelayCommand(55.0,ExportSingleCharacter(pc));
    DelayCommand(56.0,VDone());
}
