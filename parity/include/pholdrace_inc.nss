#include "parity_fx"
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetAILevel(o,AI_LEVEL_NORMAL);AssignCommand(o,ClearAllActions(TRUE));}
void Cast(object c,object t,int spell){AssignCommand(c,ActionCastSpellAtObject(spell,t,METAMAGIC_NONE,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,CLASS_TYPE_WIZARD));}
void Read(int spell,int level){
 int race;for(race=0;race<30;race++){
  if((race>=21&&race<=22)||(race>=26&&race<=28))continue;
  int count=0;int consumed=0;int n;string key="race"+IntToString(race);
  for(n=0;n<5;n++){
   string pair=key+"n"+IntToString(n);object c=GetLocalObject(GetModule(),pair+"c");object t=GetLocalObject(GetModule(),pair+"t");
   if(!GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,level,0))consumed++;
   if(GetHasSpellEffect(spell,t))count++;
  }
  PLogI(key+".consumed",consumed);PLogI(key+".anyAffected",count>0);
 }
 PDone();
}
void Setup(int spell,int level){
 int race;int index=0;
 for(race=0;race<30;race++){
  if((race>=21&&race<=22)||(race>=26&&race<=28))continue;
  int n;for(n=0;n<5;n++){
   float x=8.0+IntToFloat(index%15)*12.0;float y=8.0+IntToFloat(index/15)*12.0;index++;
   object area=GetObjectByTag("PARITY_AREA");
   object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_drowmage020",Location(area,Vector(x,y,0.0),0.0));
   object t=CreateObject(OBJECT_TYPE_CREATURE,"p_race"+IntToString(race),Location(area,Vector(x+2.0,y,0.0),0.0));
   Quiet(c);Quiet(t);ChangeToStandardFaction(c,STANDARD_FACTION_MERCHANT);ChangeToStandardFaction(t,STANDARD_FACTION_HOSTILE);
   SetIsTemporaryEnemy(c,t);SetIsTemporaryEnemy(t,c);
   ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSavingThrowDecrease(SAVING_THROW_ALL,50),t);
   SetMemorizedSpell(c,CLASS_TYPE_WIZARD,level,0,spell,TRUE,METAMAGIC_NONE);
   string key="race"+IntToString(race);
   if(n==0){PLogI(key+".race",GetRacialType(t));PLogI(key+".playable",GetIsPlayableRacialType(t));PLogI(key+".mindImmune",GetIsImmune(t,IMMUNITY_TYPE_MIND_SPELLS));PLogI(key+".paralysisImmune",GetIsImmune(t,IMMUNITY_TYPE_PARALYSIS));}
   string pair=key+"n"+IntToString(n);SetLocalObject(GetModule(),pair+"c",c);SetLocalObject(GetModule(),pair+"t",t);
   DelayCommand(8.0,Cast(c,t,spell));
  }
 }
 DelayCommand(18.0,Read(spell,level));
}
