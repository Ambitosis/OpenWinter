// Same authored spell/property on stock, crafted and cloned custom base-item rows.
#include "parity_inc"
#include "parity_time"
void Done(object c,int n){PLogF(GetTag(c)+".done"+IntToString(n),PNow()-GetLocalFloat(c,"start"),.20);SetLocalInt(c,"done",GetLocalInt(c,"done")+1);}
void Start(object c){object it=GetLocalObject(c,"item");object target=GetLocalObject(c,"target");itemproperty ip=GetFirstItemProperty(it);while(GetIsItemPropertyValid(ip)&&GetItemPropertyType(ip)!=15)ip=GetNextItemProperty(it);string k=GetTag(c);int limits=0;itemproperty lim=GetFirstItemProperty(it);while(GetIsItemPropertyValid(lim)){int kind=GetItemPropertyType(lim);if(kind>=62&&kind<=65)limits++;lim=GetNextItemProperty(it);}PLogI(k+".limits",limits);PLogI(k+".property",GetIsItemPropertyValid(ip));PLogI(k+".cost",GetItemPropertyCostTableValue(ip));PLogI(k+".seen",GetObjectSeen(target,c));PLogI(k+".equipped",GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c)==it);SetLocalFloat(c,"start",PNow());AssignCommand(c,ActionUseItemOnObject(it,ip,target));AssignCommand(c,ActionDoCommand(Done(c,1)));AssignCommand(c,ActionUseItemOnObject(it,ip,target));AssignCommand(c,ActionDoCommand(Done(c,2)));}
void Read(object c){object it=GetLocalObject(c,"item");string k=GetTag(c);PLogI(k+".impacts",GetLocalInt(c,"impacts"));PLogI(k+".done",GetLocalInt(c,"done"));PLogI(k+".valid",GetIsObjectValid(it));PLogI(k+".stack",GetItemStackSize(it));PLogI(k+".charges",GetItemCharges(it));}
void Setup(int index,string name,string res,int single,int limit,int equip){float x=IntToFloat(index%5)*12.0-24.0;float y=IntToFloat(index/5)*12.0-18.0;object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_elfmage015",PCentre(x,y));SetTag(c,name);SetAILevel(c,AI_LEVEL_NORMAL);SetPlotFlag(c,TRUE);int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);object t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(x,y+3.0));SetPlotFlag(t,TRUE);for(e=5000;e<=5012;e++)SetEventScript(t,e,"");ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);object it=CreateItemOnObject(res,c,single?3:1);itemproperty ip=GetFirstItemProperty(it);while(GetIsItemPropertyValid(ip)){RemoveItemProperty(it,ip);ip=GetFirstItemProperty(it);}AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyCastSpell(66,single?1:6),it);if(limit)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByClass(CLASS_TYPE_CLERIC),it);SetItemCharges(it,3);SetIdentified(it,TRUE);if(equip)AssignCommand(c,ActionEquipItem(it,INVENTORY_SLOT_RIGHTHAND));SetLocalObject(c,"item",it);SetLocalObject(c,"target",t);PLogI(name+".base",GetBaseItemType(it));PLogI(name+".wizard",GetLevelByClass(CLASS_TYPE_WIZARD,c));PLogI(name+".cleric",GetLevelByClass(CLASS_TYPE_CLERIC,c));PLogI(name+".umd",GetSkillRank(SKILL_USE_MAGIC_DEVICE,c));DelayCommand(5.0,Start(c));DelayCommand(20.0,Read(c));}
void main(){
 Setup(0,"potion","ct_potion",TRUE,FALSE,FALSE);
 Setup(1,"cpotion","ct_cpotion",TRUE,FALSE,FALSE);
 Setup(2,"scroll","ct_scroll",TRUE,FALSE,FALSE);
 Setup(3,"cscroll","ct_cscroll",TRUE,FALSE,FALSE);
 Setup(4,"wand","ct_wand",FALSE,FALSE,FALSE);
 Setup(5,"cwand","ct_cwand",FALSE,FALSE,FALSE);
 Setup(6,"rod","ct_rod",FALSE,FALSE,FALSE);
 Setup(7,"staff","ct_staff",FALSE,FALSE,TRUE);
 Setup(8,"xpotion","ct_xpotion",TRUE,FALSE,FALSE);
 Setup(9,"xscroll","ct_xscroll",TRUE,FALSE,FALSE);
 Setup(10,"xwand","ct_xwand",FALSE,FALSE,FALSE);
 Setup(11,"cscrollbad","ct_cscroll",TRUE,TRUE,FALSE);
 Setup(12,"cwandbad","ct_cwand",FALSE,TRUE,FALSE);
 Setup(13,"xpotionbad","ct_xpotion",TRUE,TRUE,FALSE);
 Setup(14,"xscrollbad","ct_xscroll",TRUE,TRUE,FALSE);
 Setup(15,"xwandbad","ct_xwand",FALSE,TRUE,FALSE);
 DelayCommand(21.0,PDone());
}
