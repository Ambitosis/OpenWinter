#include "parity_fx"
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetAILevel(o,AI_LEVEL_NORMAL);AssignCommand(o,ClearAllActions(TRUE));}
string EffectData(object target,int spell,object caster){
 string data="";effect e=GetFirstEffect(target);
 while(GetIsEffectValid(e)){
  if(GetEffectSpellId(e)==spell){
   data+=PFxShort(e)+":duration"+IntToString(GetEffectDuration(e))+":cl"+IntToString(GetEffectCasterLevel(e))+":creator"+IntToString(GetEffectCreator(e)==caster)+";";
  }
  e=GetNextEffect(target);
 }
 return data;
}
int Immunity(int mode){switch(mode){case 1:return IMMUNITY_TYPE_MIND_SPELLS;case 2:return IMMUNITY_TYPE_PARALYSIS;case 3:return IMMUNITY_TYPE_BLINDNESS;case 4:return IMMUNITY_TYPE_DEAFNESS;case 5:return IMMUNITY_TYPE_SLOW;case 6:return IMMUNITY_TYPE_CURSED;case 7:return IMMUNITY_TYPE_ABILITY_DECREASE;}return 0;}
void Cast(object c,object t,int spell){AssignCommand(c,ActionCastSpellAtObject(spell,t,METAMAGIC_NONE,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,CLASS_TYPE_WIZARD));}
void Read(int spell,int level){
 int mode;for(mode=0;mode<8;mode++){
  string key="immune"+IntToString(Immunity(mode));int consumed=0;int bad=0;int count=0;string sample="";string stats="";int n;
  for(n=0;n<5;n++){
   string pair=key+"n"+IntToString(n);object c=GetLocalObject(GetModule(),pair+"c");object t=GetLocalObject(GetModule(),pair+"t");
   if(!GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,level,0))consumed++;
   if(GetHasSpellEffect(spell,t)){
    count++;string data=EffectData(t,spell,c);string diff=PFxDiff(t);
    if(sample==""){sample=data;stats=diff;}
    if(sample!=data||stats!=diff)bad++;
   }
  }
  PLogI(key+".consumed",consumed);PLogI(key+".anyAffected",count>0);PLogI(key+".inconsistent",bad);
  PLog(key+".effects",sample==""?"-":sample);PLog(key+".stats",stats==""?"-":stats);
 }
 PDone();
}
void Setup(int spell,int level){
 int mode;int n;
 for(mode=0;mode<8;mode++)for(n=0;n<5;n++){
  float x=8.0+IntToFloat(n)*25.0;float y=8.0+IntToFloat(mode)*20.0;
  object area=GetObjectByTag("PARITY_AREA");
  object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_drowmage020",Location(area,Vector(x,y,0.0),0.0));
  object t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit006",Location(area,Vector(x+2.0,y,0.0),0.0));
  Quiet(c);Quiet(t);ChangeToStandardFaction(c,STANDARD_FACTION_MERCHANT);ChangeToStandardFaction(t,STANDARD_FACTION_HOSTILE);
  SetIsTemporaryEnemy(c,t);SetIsTemporaryEnemy(t,c);
  ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSavingThrowDecrease(SAVING_THROW_ALL,50),t);
  if(mode!=0)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectImmunity(Immunity(mode)),t);
  PFxSnap(t);SetMemorizedSpell(c,CLASS_TYPE_WIZARD,level,0,spell,TRUE,METAMAGIC_NONE);
  string key="immune"+IntToString(Immunity(mode));string pair=key+"n"+IntToString(n);SetLocalObject(GetModule(),pair+"c",c);SetLocalObject(GetModule(),pair+"t",t);
  DelayCommand(8.0,Cast(c,t,spell));
 }
 DelayCommand(16.0,Read(spell,level));
}
