#include "parity_fx"
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetAILevel(o,AI_LEVEL_NORMAL);AssignCommand(o,ClearAllActions(TRUE));}
void Cast(object c,object t,int spell){AssignCommand(c,ActionCastSpellAtObject(spell,t,METAMAGIC_NONE,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,CLASS_TYPE_WIZARD));}
void Read(int spell,int level){int mode;for(mode=0;mode<2;mode++){
 string key=IntToString(mode);int consumed=0;int affected=0;string sample="";int n;
 for(n=0;n<5;n++){string pair=key+"n"+IntToString(n);object c=GetLocalObject(GetModule(),pair+"c");object t=GetLocalObject(GetModule(),pair+"t");if(!GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,level,0))consumed++;if(GetHasSpellEffect(spell,t)){affected++;effect e=GetFirstEffect(t);while(GetIsEffectValid(e)){if(GetEffectSpellId(e)==spell)SetLocalInt(GetModule(),key+"type"+IntToString(GetEffectType(e)),1);e=GetNextEffect(t);}}}
 int kind;for(kind=0;kind<200;kind++)if(GetLocalInt(GetModule(),key+"type"+IntToString(kind)))sample+=IntToString(kind)+",";
 PLogI(key+".consumed",consumed);PLogI(key+".affected",affected>0);PLog(key+".types",sample==""?"-":sample);
 }PDone();}
void Setup(int spell,int level){int mode;int n;for(mode=0;mode<2;mode++)for(n=0;n<5;n++){
 float x=10.0+IntToFloat(n)*25.0;float y=10.0+IntToFloat(mode)*40.0;object area=GetObjectByTag("PARITY_AREA");
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_drowmage020",Location(area,Vector(x,y,0.0),0.0));object t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(area,Vector(x+2.0,y,0.0),0.0));
 Quiet(c);Quiet(t);ChangeToStandardFaction(c,STANDARD_FACTION_MERCHANT);ChangeToStandardFaction(t,STANDARD_FACTION_HOSTILE);SetIsTemporaryEnemy(c,t);SetIsTemporaryEnemy(t,c);
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSavingThrowDecrease(SAVING_THROW_ALL,50),t);
 int feat=spell==165?FEAT_IMMUNITY_TO_SLEEP:FEAT_AURA_OF_COURAGE;int immunity=spell==165?IMMUNITY_TYPE_SLEEP:IMMUNITY_TYPE_FEAR;
 if(mode==1)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(feat),t);
 if(n==0){PLogI(IntToString(mode)+".feat",GetHasFeat(feat,t));PLogI(IntToString(mode)+".immune",GetIsImmune(t,immunity));PLogI(IntToString(mode)+".hd",GetHitDice(t));}
 SetMemorizedSpell(c,CLASS_TYPE_WIZARD,level,0,spell,TRUE,METAMAGIC_NONE);string pair=IntToString(mode)+"n"+IntToString(n);SetLocalObject(GetModule(),pair+"c",c);SetLocalObject(GetModule(),pair+"t",t);DelayCommand(8.0,Cast(c,t,spell));
 }DelayCommand(16.0,Read(spell,level));}
