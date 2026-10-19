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
void Read(int spell,int level){
 string sample="";string stats="";int group;
 for(group=0;group<2;group++){
  int successes=0;int consumed=0;int bad=0;int n;
  for(n=0;n<20;n++){
   string key="g"+IntToString(group)+"n"+IntToString(n);
   object c=GetLocalObject(GetModule(),key+"c");object t=GetLocalObject(GetModule(),key+"t");
   if(!GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,level,0))consumed++;
   if(GetHasSpellEffect(spell,t)){
    successes++;
    string data=EffectData(t,spell,c);string diff=PFxDiff(t);
    if(sample==""){sample=data;stats=diff;}
    if(data!=sample || diff!=stats)bad++;
   }
  }
  string prefix=group==0?"lowSave":"highSave";
  PLogF(prefix+".fraction",IntToFloat(successes)/20.0,0.20);
  PLogI(prefix+".consumed",consumed);PLogI(prefix+".inconsistent",bad);
 }
 PLog("effectData",sample);PLog("statChanges",stats);PDone();
}
void Cast(object c,object t,int spell){AssignCommand(c,ActionCastSpellAtObject(spell,t,METAMAGIC_NONE,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,CLASS_TYPE_WIZARD));}
void Setup(int spell,int level){
 int group;int n;
 for(group=0;group<2;group++)for(n=0;n<20;n++){
  float x=10.0+IntToFloat(n%5)*20.0;float y=10.0+IntToFloat(n/5)*20.0+IntToFloat(group)*90.0;
  object area=GetObjectByTag("PARITY_AREA");
  object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_drowmage020",Location(area,Vector(x,y,0.0),0.0));
  object t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit006",Location(area,Vector(x+2.0,y,0.0),0.0));
  Quiet(c);Quiet(t);ChangeToStandardFaction(c,STANDARD_FACTION_MERCHANT);ChangeToStandardFaction(t,STANDARD_FACTION_HOSTILE);
  SetIsTemporaryEnemy(c,t);SetIsTemporaryEnemy(t,c);
  effect saves=group==0?EffectSavingThrowDecrease(SAVING_THROW_ALL,50):EffectSavingThrowIncrease(SAVING_THROW_ALL,50);
  ApplyEffectToObject(DURATION_TYPE_PERMANENT,saves,t);PFxSnap(t);
  SetMemorizedSpell(c,CLASS_TYPE_WIZARD,level,0,spell,TRUE,METAMAGIC_NONE);
  string key="g"+IntToString(group)+"n"+IntToString(n);SetLocalObject(GetModule(),key+"c",c);SetLocalObject(GetModule(),key+"t",t);
  DelayCommand(8.0,Cast(c,t,spell));
 }
 DelayCommand(16.0,Read(spell,level));
}
