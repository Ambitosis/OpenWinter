// Authored properties isolate activation eligibility from stock item pricing.
#include "parity_inc"
void Done(object c){SetLocalInt(c,"finished",1);}
void Start(object c){
 object it=GetLocalObject(c,"item");string k=GetTag(c);
 PLogI(k+".equipped",GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c)==it);
 itemproperty ip=GetFirstItemProperty(it);while(GetIsItemPropertyValid(ip)&&GetItemPropertyType(ip)!=ITEM_PROPERTY_CAST_SPELL)ip=GetNextItemProperty(it);
 PLogI(k+".property",GetIsItemPropertyValid(ip));PLogI(k+".identified",GetIdentified(it));
 AssignCommand(c,ActionUseItemOnObject(it,ip,c));AssignCommand(c,ActionDoCommand(Done(c)));
}
void Read(object c){
 object it=GetLocalObject(c,"item");string k=GetTag(c);
 PLogI(k+".impacts",GetLocalInt(c,"impacts"));PLogI(k+".finished",GetLocalInt(c,"finished"));
 PLogI(k+".castitem",GetLocalInt(c,"castitem"));PLogI(k+".valid",GetIsObjectValid(it));
 PLogI(k+".charges",GetItemCharges(it));PLogI(k+".stack",GetItemStackSize(it));
}
void Setup(string name,string res,int single,int limit,int equipped,int identified,float x,float y){
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_elfmage015",PCentre(x,y));SetTag(c,name);SetAILevel(c,AI_LEVEL_NORMAL);SetPlotFlag(c,TRUE);
 int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);
 object it=CreateItemOnObject(res,c,single?3:1);SetLocalObject(c,"item",it);
 itemproperty ip=GetFirstItemProperty(it);while(GetIsItemPropertyValid(ip)){RemoveItemProperty(it,ip);ip=GetFirstItemProperty(it);}
 AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyCastSpell(167,single?1:6),it);
 if(limit==1)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByClass(CLASS_TYPE_WIZARD),it);
 if(limit==2)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByClass(CLASS_TYPE_CLERIC),it);
 if(limit==3)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByRace(RACIAL_TYPE_HALFORC),it);
 if(limit==4)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByAlign(IP_CONST_ALIGNMENTGROUP_EVIL),it);
 SetItemCharges(it,3);SetIdentified(it,identified);if(equipped)AssignCommand(c,ActionEquipItem(it,INVENTORY_SLOT_RIGHTHAND));
 if(name=="stafflocked")DelayCommand(1.0,AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByClass(CLASS_TYPE_CLERIC),it));
 PLogI(name+".wizard",GetLevelByClass(CLASS_TYPE_WIZARD,c));PLogI(name+".cleric",GetLevelByClass(CLASS_TYPE_CLERIC,c));
 PLogI(name+".race",GetRacialType(c));PLogI(name+".good",GetGoodEvilValue(c));PLogI(name+".umd",GetSkillRank(SKILL_USE_MAGIC_DEVICE,c));
 PLogI(name+".base",GetBaseItemType(it));
 DelayCommand(4.0,Start(c));DelayCommand(12.0,Read(c));
}
void main(){
 Setup("staffbag","nw_wmgst004",FALSE,0,FALSE,TRUE,-15.0,-15.0);
 Setup("staffhand","nw_wmgst004",FALSE,0,TRUE,TRUE,-10.0,-15.0);
 Setup("wandplain","nw_wmgwn004",FALSE,0,FALSE,TRUE,-5.0,-15.0);
 Setup("wandclassok","nw_wmgwn004",FALSE,1,FALSE,TRUE,0.0,-15.0);
 Setup("wandclassbad","nw_wmgwn004",FALSE,2,FALSE,TRUE,5.0,-15.0);
 Setup("wandracebad","nw_wmgwn004",FALSE,3,FALSE,TRUE,10.0,-15.0);
 Setup("wandalignment","nw_wmgwn004",FALSE,4,FALSE,TRUE,15.0,-15.0);
 Setup("wandunknown","nw_wmgwn004",FALSE,0,FALSE,FALSE,-15.0,-5.0);
 Setup("scrollplain","nw_it_sparscr104",TRUE,0,FALSE,TRUE,-10.0,-5.0);
 Setup("scrollclassok","nw_it_sparscr104",TRUE,1,FALSE,TRUE,-5.0,-5.0);
 Setup("scrollclassbad","nw_it_sparscr104",TRUE,2,FALSE,TRUE,0.0,-5.0);
 Setup("scrollracebad","nw_it_sparscr104",TRUE,3,FALSE,TRUE,5.0,-5.0);
 Setup("scrollalign","nw_it_sparscr104",TRUE,4,FALSE,TRUE,10.0,-5.0);
 Setup("scrollunknown","nw_it_sparscr104",TRUE,0,FALSE,FALSE,15.0,-5.0);
 Setup("potionclassbad","nw_it_mpotion001",TRUE,2,FALSE,TRUE,-15.0,5.0);
 Setup("potionunknown","nw_it_mpotion001",TRUE,0,FALSE,FALSE,-10.0,5.0);
 Setup("staffclassbad","nw_wmgst004",FALSE,2,TRUE,TRUE,-5.0,5.0);
 Setup("staffunknown","nw_wmgst004",FALSE,0,TRUE,FALSE,0.0,5.0);
 Setup("potionracebad","nw_it_mpotion001",TRUE,3,FALSE,TRUE,5.0,5.0);
 Setup("potionalign","nw_it_mpotion001",TRUE,4,FALSE,TRUE,10.0,5.0);
 Setup("stafflocked","nw_wmgst004",FALSE,0,TRUE,TRUE,15.0,5.0);
 Setup("rodplain","nw_wmgmrd006",FALSE,0,FALSE,TRUE,-15.0,15.0);
 Setup("rodclassbad","nw_wmgmrd006",FALSE,2,FALSE,TRUE,-10.0,15.0);
 Setup("rodracebad","nw_wmgmrd006",FALSE,3,FALSE,TRUE,-5.0,15.0);
 Setup("rodalign","nw_wmgmrd006",FALSE,4,FALSE,TRUE,0.0,15.0);
 DelayCommand(13.0,PDone());
}
