// Stock item-family timing. Impact scripts are authored observers only;
// this suite proves action/resource timing, not the original spell statistics.
#include "parity_inc"
#include "parity_time"
int Remaining(object c){object it=GetLocalObject(c,"item");if(!GetIsObjectValid(it))return 0;int mode=GetLocalInt(c,"resource");if(mode==1)return GetItemStackSize(it);if(mode==2)return GetItemCharges(it);return 1;}
void Finished(object c,int n){string k=GetTag(c)+".done"+IntToString(n);PLogF(k,PNow()-GetLocalFloat(c,"start"),.20);PLogI(k+".remaining",Remaining(c));PLogI(k+".combat",GetIsInCombat(c));SetLocalInt(c,"finishes",GetLocalInt(c,"finishes")+1);}
void Use(object c,int n){object it=GetLocalObject(c,"item");itemproperty ip=GetFirstItemProperty(it);while(GetIsItemPropertyValid(ip)&&!(GetItemPropertyType(ip)==15&&GetItemPropertySubType(ip)==GetLocalInt(c,"subtype")))ip=GetNextItemProperty(it);AssignCommand(c,ActionUseItemOnObject(it,ip,GetLocalObject(c,"target")));AssignCommand(c,ActionDoCommand(Finished(c,n)));}
void Poll(object c,int prior,int n){int value=Remaining(c);if(value!=prior){int count=GetLocalInt(c,"changes")+1;SetLocalInt(c,"changes",count);string k=GetTag(c)+".spent"+IntToString(count);PLogF(k,PNow()-GetLocalFloat(c,"start"),.15);PLogI(k+".remaining",value);}if(n<240)DelayCommand(.05,Poll(c,value,n+1));}
void Start(object c){PLogI(GetTag(c)+".equipped",GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c)==GetLocalObject(c,"item"));object target=GetLocalObject(c,"target");PLogI(GetTag(c)+".seen",GetObjectSeen(target,c));SetLocalFloat(c,"start",PNow());Poll(c,Remaining(c),0);Use(c,1);}
void Read(object c){string k=GetTag(c);PLogI(k+".impacts",GetLocalInt(c,"impacts"));PLogI(k+".finishes",GetLocalInt(c,"finishes"));PLogI(k+".remaining",Remaining(c));PLogI(k+".action",GetCurrentAction(c));}
void Setup(string name,string res,int sub,int mode,float x,int hostile){
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_elfmage015",PCentre(x,0.0));SetTag(c,name);SetAILevel(c,AI_LEVEL_NORMAL);SetPlotFlag(c,TRUE);int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);
 object target=c;if(hostile){target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(x,3.0));SetPlotFlag(target,TRUE);for(e=5000;e<=5012;e++)SetEventScript(target,e,"");ChangeToStandardFaction(target,STANDARD_FACTION_HOSTILE);SetIsTemporaryEnemy(c,target);SetIsTemporaryEnemy(target,c);}
 object it=CreateItemOnObject(res,c,mode==1?3:1);if(mode==2)SetItemCharges(it,3);
 itemproperty old=GetFirstItemProperty(it);while(GetIsItemPropertyValid(old)){RemoveItemProperty(it,old);old=GetFirstItemProperty(it);}AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyCastSpell(sub,mode==1?1:6),it);
 SetLocalObject(c,"item",it);SetLocalObject(c,"target",target);SetLocalInt(c,"subtype",sub);SetLocalInt(c,"resource",mode);
 itemproperty ip=GetFirstItemProperty(it);while(GetIsItemPropertyValid(ip)&&!(GetItemPropertyType(ip)==15&&GetItemPropertySubType(ip)==sub))ip=GetNextItemProperty(it);
 PLogI(name+".valid",GetIsObjectValid(it));PLogI(name+".class",GetLevelByClass(CLASS_TYPE_WIZARD,c));PLogI(name+".base",GetBaseItemType(it));PLogI(name+".property",GetIsItemPropertyValid(ip));PLogI(name+".subtype",GetItemPropertySubType(ip));PLogI(name+".cost",GetItemPropertyCostTableValue(ip));PLogI(name+".initial",Remaining(c));
 DelayCommand(2.0,Start(c));DelayCommand(15.0,Read(c));
}
void main(){int i;for(i=0;i<4;i++){Setup("scroll"+IntToString(i),"nw_it_sparscr104",66+i,1,IntToFloat(i)*7.0-25.0,FALSE);Setup("wand"+IntToString(i),"nw_wmgwn004",66+i,2,IntToFloat(i)*7.0+4.0,FALSE);}DelayCommand(16.0,PDone());}
