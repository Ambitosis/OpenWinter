#include "parity_inc"
void State(object c,string tag,string when) {
 PLogI(tag+"."+when+".valid",GetIsObjectValid(c));
 if(GetIsObjectValid(c)) { PLogI(tag+"."+when+".loot",GetLootable(c));PLogI(tag+"."+when+".select",GetIsSelectableWhenDead(c)); }
}
void NearBag(object c,string tag) {
 object o=GetFirstObjectInArea(GetObjectByTag("PARITY_AREA"));int n=0;
 while(GetIsObjectValid(o)) {if(GetObjectType(o)==OBJECT_TYPE_PLACEABLE && GetDistanceBetween(c,o)<1.0){n++;PLog(tag+".bag",GetName(o)+":"+IntToString(GetAppearanceType(o)));}o=GetNextObjectInArea(GetObjectByTag("PARITY_AREA"));}
 PLogI(tag+".bags",n);
}
void Trial(string tag,float x,int loot,int destroy,int select) {
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(x,0.0),FALSE,tag);
 int i;for(i=5000;i<=5012;i++)SetEventScript(c,i,"");
 SetLootable(c,loot);SetIsDestroyable(destroy,TRUE,select,c);
 object item=CreateItemOnObject("nw_it_mpotion001",c);SetDroppableFlag(item,TRUE);
 ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),c);
 State(c,tag,"dead");DelayCommand(1.0,NearBag(c,tag+".1s"));DelayCommand(2.0,NearBag(c,tag+".2s"));DelayCommand(2.5,NearBag(c,tag+".2_5s"));DelayCommand(3.0,NearBag(c,tag+".3s"));DelayCommand(4.0,NearBag(c,tag+".4s"));DelayCommand(4.0,State(c,tag,"4s"));DelayCommand(7.0,State(c,tag,"7s"));DelayCommand(12.0,State(c,tag,"12s"));
}
void Bags() {object o=GetFirstObjectInArea(GetObjectByTag("PARITY_AREA"));int n=0;while(GetIsObjectValid(o)) {if(GetObjectType(o)==OBJECT_TYPE_PLACEABLE) {n++;PLog("bag"+IntToString(n),GetTag(o)+":"+GetName(o)+":"+IntToString(GetAppearanceType(o))+":"+IntToString(GetIsObjectValid(GetFirstItemInInventory(o))));} o=GetNextObjectInArea(GetObjectByTag("PARITY_AREA"));}PLogI("bags",n);PDone();}
void main(){Trial("normal",0.0,FALSE,TRUE,FALSE);Trial("loot",5.0,TRUE,TRUE,FALSE);Trial("select",10.0,FALSE,TRUE,TRUE);Trial("persist",15.0,FALSE,FALSE,FALSE);DelayCommand(13.0,Bags());}
