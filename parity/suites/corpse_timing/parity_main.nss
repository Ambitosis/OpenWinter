#include "parity_inc"
void State(object c,string tag,int tenth) {
 PLogI(tag+"."+IntToString(tenth),GetIsObjectValid(c));
}
void Trial(string tag,float x,string res,int spectacular,int loot,int destroy) {
 object c=CreateObject(OBJECT_TYPE_CREATURE,res,PCentre(x,0.0),FALSE,tag);
 int i;for(i=5000;i<=5012;i++)SetEventScript(c,i,"");
 SetLootable(c,loot);SetIsDestroyable(destroy,TRUE,FALSE,c);
 object item=CreateItemOnObject("nw_it_mpotion001",c);SetDroppableFlag(item,TRUE);
 ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(spectacular),c);
 for(i=10;i<=70;i+=5) DelayCommand(IntToFloat(i)/10.0,State(c,tag,i));
}
void Bags() {
 object o=GetFirstObjectInArea(GetObjectByTag("PARITY_AREA"));int n=0;
 while(GetIsObjectValid(o)) {
  if(GetObjectType(o)==OBJECT_TYPE_PLACEABLE){ n++;PLog("bag"+IntToString(n),GetName(o)+":"+IntToString(GetAppearanceType(o))); }
  o=GetNextObjectInArea(GetObjectByTag("PARITY_AREA"));
 }
 PLogI("bags",n);PDone();
}
void main(){
 Trial("normal",0.0,"nw_bandit001",FALSE,FALSE,TRUE);
 Trial("spectacular",5.0,"nw_bandit001",TRUE,FALSE,TRUE);
 Trial("skeleton",10.0,"nw_skeleton",FALSE,FALSE,TRUE);
 Trial("skeleton_spectacular",15.0,"nw_skeleton",TRUE,FALSE,TRUE);
 Trial("persistent_loot",20.0,"nw_bandit001",FALSE,TRUE,FALSE);
 DelayCommand(10.0,Bags());
}
