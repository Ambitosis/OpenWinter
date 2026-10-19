#include "parity_fx"
void Quiet(object o) { int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetAILevel(o,AI_LEVEL_NORMAL);AssignCommand(o,ClearAllActions(TRUE)); }
object Actor(string blueprint,float x,float y) {
 object o=CreateObject(OBJECT_TYPE_CREATURE,blueprint,Location(GetObjectByTag("PARITY_AREA"),Vector(x,y,0.0),0.0));Quiet(o);return o;
}
void Read(string key,int adjustment) {
 object first=GetLocalObject(GetModule(),key+"caster0");
 int count=0;int bad=0;int i;
 for(i=0;i<80;i++) {
  object c=GetLocalObject(GetModule(),key+"caster"+IntToString(i/4));
  object t=GetLocalObject(c,"t"+IntToString(i%4));
  if(GetDistanceBetween(c,t)>3.0)bad++;
  if(GetHasSpellEffect(SPELL_BATTLETIDE,t)) {
   count++;
   if(GetWillSavingThrow(t)!=GetLocalInt(t,"before")-(adjustment<0?0:2))bad++;
   if(GetFortitudeSavingThrow(t)!=GetLocalInt(t,"fort")-2)bad++;
   if(GetReflexSavingThrow(t)!=GetLocalInt(t,"ref")-2)bad++;
   int effects=0;int kinds=0;effect e=GetFirstEffect(t);
   while(GetIsEffectValid(e)) {
    if(GetEffectSpellId(e)==SPELL_BATTLETIDE) {
     effects++;
     if(GetEffectCreator(e)!=c || GetEffectCasterLevel(e)!=20 || GetEffectDurationType(e)!=DURATION_TYPE_PERMANENT)bad++;
     int type=GetEffectType(e);int amount=GetEffectInteger(e,0);
     if(type==41 && amount==2)kinds=kinds|1;
     if(type==43 && amount==2 && GetEffectInteger(e,1)==8)kinds=kinds|2;
     if(type==51 && amount==2 && GetEffectInteger(e,1)==0)kinds=kinds|4;
     if(type==74 && amount==207)kinds=kinds|8;
    }
    e=GetNextEffect(t);
   }
   if(effects!=4 || kinds!=15)bad++;
  }
 }
 PLogF(key+".debuffFraction",IntToFloat(count)/80.0,0.15);
 PLogI(key+".badEffectData",bad);
 int buffed=0;int used=0;int badCaster=0;
 for(i=0;i<20;i++) {
  object caster=GetLocalObject(GetModule(),key+"caster"+IntToString(i));
  if(GetHasSpellEffect(SPELL_BATTLETIDE,caster))buffed++;
  if(!GetMemorizedSpellReady(caster,CLASS_TYPE_CLERIC,5,0))used++;
  if(GetWillSavingThrow(caster)!=GetLocalInt(caster,"will")+2 || GetFortitudeSavingThrow(caster)!=GetLocalInt(caster,"fort")+2 || GetReflexSavingThrow(caster)!=GetLocalInt(caster,"ref")+2)badCaster++;
  int kinds=0;effect e=GetFirstEffect(caster);
  while(GetIsEffectValid(e)) {
   if(GetEffectSpellId(e)==SPELL_BATTLETIDE) {
    int type=GetEffectType(e);int amount=GetEffectInteger(e,0);
    if(type==40 && amount==2)kinds=kinds|1;
    if(type==42 && amount==2 && GetEffectInteger(e,1)==8)kinds=kinds|2;
    if(type==50 && amount==2 && GetEffectInteger(e,1)==0)kinds=kinds|4;
    if(type==74 && amount==206)kinds=kinds|8;
   }
   e=GetNextEffect(caster);
  }
  if(kinds!=15)badCaster++;
 }
 PLogI(key+".badCasterEffects",badCaster);PLogI(key+".casterBuffs",buffed);PLogI(key+".consumed",used);
 object o=GetFirstObjectInArea(GetArea(first));
 while(GetIsObjectValid(o)) {
  if(GetObjectType(o)==OBJECT_TYPE_AREA_OF_EFFECT && GetAreaOfEffectCreator(o)==first) {
   SetLocalString(o,"key",key);ExecuteScript("bts_context",o);
  }
  o=GetNextObjectInArea(GetArea(first));
 }
}
void Cast(object c) { AssignCommand(c,ActionCastSpellAtObject(SPELL_BATTLETIDE,c,METAMAGIC_NONE,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,CLASS_TYPE_CLERIC)); }
void Group(int group,int adjustment) {
 string key=group==0?"lowSave":"highSave";
 int n;for(n=0;n<20;n++) {
  float x=20.0+IntToFloat(n%5)*16.0;float y=20.0+IntToFloat(n/5)*16.0+IntToFloat(group)*80.0;
  object c=Actor("nw_drowcler020",x,y);ChangeToStandardFaction(c,STANDARD_FACTION_MERCHANT);
  SetLocalObject(GetModule(),key+"caster"+IntToString(n),c);
  SetLocalInt(c,"will",GetWillSavingThrow(c));SetLocalInt(c,"fort",GetFortitudeSavingThrow(c));SetLocalInt(c,"ref",GetReflexSavingThrow(c));
  int i;for(i=0;i<4;i++) {
   float dx=i==0?2.0:i==1?-2.0:0.0;float dy=i==2?2.0:i==3?-2.0:0.0;
   object t=Actor("nw_bandit006",x+dx,y+dy);
   ChangeToStandardFaction(t,STANDARD_FACTION_HOSTILE);SetIsTemporaryEnemy(t,c);SetIsTemporaryEnemy(c,t);
   effect e=adjustment<0?EffectSavingThrowDecrease(SAVING_THROW_WILL,-adjustment):EffectSavingThrowIncrease(SAVING_THROW_WILL,adjustment);
   ApplyEffectToObject(DURATION_TYPE_PERMANENT,e,t);
   SetLocalInt(t,"before",GetWillSavingThrow(t));SetLocalInt(t,"fort",GetFortitudeSavingThrow(t));SetLocalInt(t,"ref",GetReflexSavingThrow(t));
   SetLocalObject(c,"t"+IntToString(i),t);
  }
  SetMemorizedSpell(c,CLASS_TYPE_CLERIC,5,0,SPELL_BATTLETIDE,TRUE,METAMAGIC_NONE);
  DelayCommand(9.0,Cast(c));
 }
 DelayCommand(18.0,Read(key,adjustment));
}
void main(){Group(0,-50);Group(1,50);DelayCommand(20.0,PDone());}
