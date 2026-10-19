#include "pcombat_inc"
#include "parity_time"
void Mark(object o,int n){PLogF(GetTag(o)+".after"+IntToString(n),PNow()-GetLocalFloat(o,"start"),0.2);}
void Begin(object o,int meta){
 string k=GetTag(o);SetLocalFloat(o,"start",PNow());
 PLogI(k+".pc",GetIsPC(o));PLogI(k+".level",GetLevelByClass(CLASS_TYPE_WIZARD,o));
 PLogI(k+".quickenFeat",GetHasFeat(FEAT_QUICKEN_SPELL,o));
 int h=0;effect fx=GetFirstEffect(o);while(GetIsEffectValid(fx)){if(GetEffectType(fx)==EFFECT_TYPE_HASTE)h=1;fx=GetNextEffect(o);}
 PLogI(k+".hasteActual",h);
 int level=1;if(meta==METAMAGIC_QUICKEN)level=5;
 PLogI(k+".preparedId",GetMemorizedSpellId(o,CLASS_TYPE_WIZARD,level,0));
 PLogI(k+".preparedReady",GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,level,0));
 PLogI(k+".preparedMeta",GetMemorizedSpellMetaMagic(o,CLASS_TYPE_WIZARD,level,0));
 PLogI(k+".defensive",GetActionMode(o,ACTION_MODE_DEFENSIVE_CAST));PLogI(k+".combatStart",GetIsInCombat(o));PLogI(k+".attacksBefore",GetLocalInt(o,"attacks")>0);
 int i;for(i=0;i<3;i++){AssignCommand(o,ActionCastSpellAtObject(0,o,meta,FALSE));AssignCommand(o,ActionDoCommand(Mark(o,i)));}
}
void Report(object o){
 string k=GetTag(o);PLogI(k+".impacts",GetLocalInt(o,"impacts"));PLogI(k+".combatEnd",GetIsInCombat(o));
 int i;for(i=0;i<3;i++){PLogI(k+".normalReady"+IntToString(i),GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,1,i));PLogI(k+".quickReady"+IntToString(i),GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,5,i));}
}
void Fight(object e,object c){PLogI(GetTag(c)+".enemy",GetIsEnemy(c,e));AssignCommand(e,ActionAttack(c));}
void Case(int n){
 float x=5.0+IntToFloat(n%4)*18.0;float y=10.0+IntToFloat(n/4)*40.0;
 object c=PSpawn("cad_wiz",x,y);SetTag(c,IntToString(n));SetPlotFlag(c,FALSE);SetImmortal(c,TRUE);
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDamageImmunityIncrease(DAMAGE_TYPE_BLUDGEONING,100),c);
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDamageImmunityIncrease(DAMAGE_TYPE_PIERCING,100),c);
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDamageImmunityIncrease(DAMAGE_TYPE_SLASHING,100),c);
 SetEventScript(c,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"cad_attacked");ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSkillIncrease(SKILL_CONCENTRATION,50),c);SetActionMode(c,ACTION_MODE_DEFENSIVE_CAST,TRUE);
 int mode=n%4;int haste=mode==1||mode==3;int quick=mode==2||mode==3;
 int meta=METAMAGIC_NONE;if(quick)meta=METAMAGIC_QUICKEN;
 PLogI(IntToString(n)+".hasteRequested",haste);
 if(haste)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);
 if(n>=4){object e=PSpawn("nw_bandit001",x+1.5,y);SetPlotFlag(e,FALSE);SetImmortal(e,TRUE);ChangeToStandardFaction(e,STANDARD_FACTION_HOSTILE);ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);SetIsTemporaryEnemy(e,c,FALSE,60.0);SetIsTemporaryEnemy(c,e,FALSE,60.0);DelayCommand(8.0,Fight(e,c));}
 float t=12.0+IntToFloat(n)*0.1;DelayCommand(t,Begin(c,meta));DelayCommand(t+23.0,Report(c));
}
void main(){int i;for(i=0;i<8;i++)Case(i);DelayCommand(38.0,PDone());}
