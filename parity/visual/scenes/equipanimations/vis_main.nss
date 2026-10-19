#include "visual_inc"
#include "ea_inc"
object Actor(object pc,string tag,float x)
{
    vector p=GetPosition(pc);location at=Location(GetArea(pc),Vector(p.x+x,p.y+1.5,p.z),270.0);
    object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",at);
    SetTag(c,tag);SetLocalLocation(c,"origin",at);
    int i;for(i=5000;i<=5013;i++)SetEventScript(c,i,"");
    AssignCommand(c,ClearAllActions(TRUE));SetAILevel(c,AI_LEVEL_NORMAL);
    ChangeToStandardFaction(c,STANDARD_FACTION_MERCHANT);SetPlotFlag(c,TRUE);
    object item=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c);if(GetIsObjectValid(item))DestroyObject(item);
    item=GetItemInSlot(INVENTORY_SLOT_LEFTHAND,c);if(GetIsObjectValid(item))DestroyObject(item);
    SetLocalObject(c,"club",CreateItemOnObject("nw_wblcl001",c,1,tag+"_CLUB"));
    SetLocalObject(c,"shield",CreateItemOnObject("nw_ashsw001",c,1,tag+"_SHIELD"));
    return c;
}
void Finished(object c,int phase)
{
    SetLocalInt(c,"completed",GetLocalInt(c,"completed")+1);
    EAState(c,"queue_finished_"+IntToString(phase));
}
void Execute(object c,int phase)
{
    SetLocalFloat(c,"start",EANow());EAState(c,"before_"+IntToString(phase));SetLocalInt(c,"phase",phase);
    object item=GetLocalObject(c,phase<3?"club":"shield");
    if(phase==1)AssignCommand(c,ActionEquipItem(item,INVENTORY_SLOT_RIGHTHAND));
    else if(phase==3)AssignCommand(c,ActionEquipItem(item,INVENTORY_SLOT_LEFTHAND));
    else AssignCommand(c,ActionUnequipItem(item));
    AssignCommand(c,ActionDoCommand(Finished(c,phase)));
}
void Shot(object a,object b,object c,string name)
{
    EAState(a,name);EAState(b,name);EAState(c,name);VShot(name);
}
void Wave(object a,object b,object c,int phase,float time,string name)
{
    DelayCommand(time,Execute(a,phase));DelayCommand(time,Execute(b,phase));DelayCommand(time,Execute(c,phase));
    DelayCommand(time+.20,Shot(a,b,c,name+"_early"));DelayCommand(time+1.60,Shot(a,b,c,name+"_settled"));
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"started"))return;
    SetLocalInt(GetModule(),"started",1);VBegin(pc);VHidePC(pc);
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_EQUIP_ITEM,"ea_equip");
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_UNEQUIP_ITEM,"ea_unequip");
    object a=Actor(pc,"STOCK",-1.7),b=Actor(pc,"RIGHT",0.0),c=Actor(pc,"LEFT",1.7);
    ReplaceObjectAnimation(b,"drwright","salute");ReplaceObjectAnimation(c,"drwleft","salute");
    EANote("replacement=RIGHT:drwright->salute,LEFT:drwleft->salute|actors=nw_bandit001|events=module_PC_callbacks_NPC_zero_expected");
    DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));DelayCommand(3.0,Shot(a,b,c,"a_empty"));
    Wave(a,b,c,1,5.0,"b_main_equip");Wave(a,b,c,2,8.0,"c_main_remove");
    Wave(a,b,c,3,11.0,"d_left_equip");Wave(a,b,c,4,14.0,"e_left_remove");
    DelayCommand(17.0,VDone());
}
