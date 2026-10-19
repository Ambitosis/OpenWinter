#include "parity_fx"
void AddCurse(object target){ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectCurse(2,2,2,2,2,2),target);}
void AddImmunity(object target){ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectImmunity(IMMUNITY_TYPE_ABILITY_DECREASE),target,2.0);}
void CopyCurse(){object a=GetLocalObject(GetModule(),"t8");object b=GetLocalObject(GetModule(),"t9");effect e=GetFirstEffect(a);while(GetIsEffectValid(e)){if(GetEffectType(e)==EFFECT_TYPE_CURSE){ApplyEffectToObject(DURATION_TYPE_PERMANENT,e,b);return;}e=GetNextEffect(a);}}
void Read(string phase){int i;for(i=0;i<10;i++){object t=GetLocalObject(GetModule(),"t"+IntToString(i));string key=IntToString(i)+"."+phase;PLog(key+".stats",PFxDiff(t));PLog(key+".effects",PFxTypes(t));}}
void main(){
 int i;for(i=0;i<10;i++){
  object t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit006",PCentre(IntToFloat(i)*3.0,0.0));
  int eventId;for(eventId=5000;eventId<=5012;eventId++)SetEventScript(t,eventId,"");
  SetLocalObject(GetModule(),"t"+IntToString(i),t);PFxSnap(t);
  if(i==0){AddImmunity(t);AddCurse(t);}
  if(i==1){AddCurse(t);DelayCommand(1.0,AddImmunity(t));}
  if(i==2){AddImmunity(t);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectAbilityDecrease(ABILITY_STRENGTH,2),t);}
  if(i==4){AddImmunity(t);ApplyEffectToObject(DURATION_TYPE_PERMANENT,IgnoreEffectImmunity(EffectCurse(2,2,2,2,2,2)),t);}
  if(i==5){ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectLinkEffects(EffectImmunity(IMMUNITY_TYPE_ABILITY_DECREASE),EffectCurse(2,2,2,2,2,2)),t);}
  if(i==6){ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectLinkEffects(EffectCurse(2,2,2,2,2,2),EffectImmunity(IMMUNITY_TYPE_ABILITY_DECREASE)),t);}
  if(i==7){AddImmunity(t);ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectCurse(2,2,2,2,2,2),t);}
  if(i==8){AddImmunity(t);AddCurse(t);}
  if(i==3){ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectAbilityDecrease(ABILITY_STRENGTH,2),t);DelayCommand(1.0,AddImmunity(t));}
 }
 DelayCommand(3.0,CopyCurse());
 DelayCommand(0.25,Read("start"));DelayCommand(1.25,Read("immune"));DelayCommand(2.25,Read("earlyExpiry"));DelayCommand(3.25,Read("expired"));DelayCommand(3.5,PDone());
}
