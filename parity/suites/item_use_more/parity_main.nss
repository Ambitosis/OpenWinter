// Stock item-family timing. Impact scripts are authored observers only;
// this suite proves action/resource timing, not the original spell statistics.
#include "parity_inc"
#include "parity_time"
int Remaining(object c){object it=GetLocalObject(c,"item");if(!GetIsObjectValid(it))return 0;int mode=GetLocalInt(c,"resource");if(mode==1)return GetItemStackSize(it);if(mode==2)return GetItemCharges(it);return 1;}
void Finished(object c,int n){string k=GetTag(c)+".done"+IntToString(n);PLogF(k,PNow()-GetLocalFloat(c,"start"),.20);PLogI(k+".remaining",Remaining(c));PLogI(k+".combat",GetIsInCombat(c));SetLocalInt(c,"finishes",GetLocalInt(c,"finishes")+1);}
void Use(object c,int n){object it=GetLocalObject(c,"item");itemproperty ip=GetFirstItemProperty(it);while(GetIsItemPropertyValid(ip)&&!(GetItemPropertyType(ip)==15&&GetItemPropertySubType(ip)==GetLocalInt(c,"subtype")))ip=GetNextItemProperty(it);AssignCommand(c,ActionUseItemOnObject(it,ip,GetLocalObject(c,"target")));AssignCommand(c,ActionDoCommand(Finished(c,n)));}
void Poll(object c,int prior,int n){int value=Remaining(c);if(value!=prior){int count=GetLocalInt(c,"changes")+1;SetLocalInt(c,"changes",count);string k=GetTag(c)+".spent"+IntToString(count);PLogF(k,PNow()-GetLocalFloat(c,"start"),.15);PLogI(k+".remaining",value);}if(n<560)DelayCommand(.05,Poll(c,value,n+1));}
int Hasted(object c){effect e=GetFirstEffect(c);while(GetIsEffectValid(e)){if(GetEffectType(e)==EFFECT_TYPE_HASTE)return TRUE;e=GetNextEffect(c);}return FALSE;}
void Start(object c){PLogI(GetTag(c)+".equipped",GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c)==GetLocalObject(c,"item"));PLogI(GetTag(c)+".haste",Hasted(c));object target=GetLocalObject(c,"target");PLogI(GetTag(c)+".seen",GetObjectSeen(target,c));SetLocalFloat(c,"start",PNow());Poll(c,Remaining(c),0);Use(c,1);Use(c,2);Use(c,3);}
void Read(object c){string k=GetTag(c);PLogI(k+".impacts",GetLocalInt(c,"impacts"));PLogI(k+".finishes",GetLocalInt(c,"finishes"));PLogI(k+".remaining",Remaining(c));PLogI(k+".action",GetCurrentAction(c));}
void Setup(string name,string res,int sub,int mode,float x,int hostile,int equipped,int hasted){
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_elfmage015",PCentre(x,0.0));SetTag(c,name);SetAILevel(c,AI_LEVEL_NORMAL);SetPlotFlag(c,TRUE);int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);
 object target=c;if(hostile){target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(x,3.0));SetPlotFlag(target,TRUE);for(e=5000;e<=5012;e++)SetEventScript(target,e,"");ChangeToStandardFaction(target,STANDARD_FACTION_HOSTILE);SetIsTemporaryEnemy(c,target);SetIsTemporaryEnemy(target,c);}
 object it=CreateItemOnObject(res,c,mode==1?3:1);if(mode==2)SetItemCharges(it,3);
 if(equipped)AssignCommand(c,ActionEquipItem(it,INVENTORY_SLOT_RIGHTHAND));if(hasted)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);
 SetLocalObject(c,"item",it);SetLocalObject(c,"target",target);SetLocalInt(c,"subtype",sub);SetLocalInt(c,"resource",mode);
 itemproperty ip=GetFirstItemProperty(it);while(GetIsItemPropertyValid(ip)&&!(GetItemPropertyType(ip)==15&&GetItemPropertySubType(ip)==sub))ip=GetNextItemProperty(it);
 PLogI(name+".valid",GetIsObjectValid(it));PLogI(name+".class",GetLevelByClass(CLASS_TYPE_WIZARD,c));PLogI(name+".base",GetBaseItemType(it));PLogI(name+".property",GetIsItemPropertyValid(ip));PLogI(name+".subtype",GetItemPropertySubType(ip));PLogI(name+".cost",GetItemPropertyCostTableValue(ip));PLogI(name+".initial",Remaining(c));
 DelayCommand(8.0,Start(c));DelayCommand(36.5,Read(c));
}
void main(){
 Setup("staff_equipped","nw_wmgst004",167,2,-14.0,FALSE,TRUE,FALSE);
 Setup("scroll_extra","nw_it_sparscr104",167,1,-7.0,FALSE,FALSE,FALSE);
 Setup("scroll_mm","nw_it_sparscr109",172,1,0.0,TRUE,FALSE,FALSE);
 Setup("scroll_haste","nw_it_sparscr104",167,1,7.0,FALSE,FALSE,TRUE);
 Setup("wand_haste","nw_wmgwn004",172,2,14.0,TRUE,FALSE,TRUE);
 DelayCommand(37.0,PDone());}
