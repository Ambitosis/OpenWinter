#include "pcombat_inc"
#include "parity_time"
void AfterSpell(object c){PLogF(GetTag(c)+".afterSpell",PNow()-GetLocalFloat(c,"start"),0.2);}
void SwitchToSpell(object c,int meta){
 PLogF(GetTag(c)+".switch",PNow()-GetLocalFloat(c,"start"),0.2);
 PLogI(GetTag(c)+".switchCombat",GetIsInCombat(c));PLogI(GetTag(c)+".switchSwung",GetLocalInt(c,"swings")>0);
 AssignCommand(c,ClearAllActions(TRUE));AssignCommand(c,ActionCastSpellAtObject(0,c,meta,FALSE));AssignCommand(c,ActionDoCommand(AfterSpell(c)));
}
void Begin(object c,object t,int n){
 SetLocalFloat(c,"start",PNow());int meta=METAMAGIC_NONE;if(n==1||n==3||n>=8)meta=METAMAGIC_QUICKEN;
 if(n<4){AssignCommand(c,ActionCastSpellAtObject(0,c,meta,FALSE));AssignCommand(c,ActionDoCommand(AfterSpell(c)));AssignCommand(c,ActionAttack(t));}
 else{AssignCommand(c,ActionAttack(t));float delay=1.0+IntToFloat(n%4)*2.0;DelayCommand(delay,SwitchToSpell(c,meta));}
}
void Report(object c){PLogI(GetTag(c)+".impacts",GetLocalInt(c,"impacts"));PLogI(GetTag(c)+".swung",GetLocalInt(c,"swings")>0);}
void main(){int n;for(n=0;n<12;n++){
 float x=5.0+IntToFloat(n%4)*18.0;float y=5.0+IntToFloat(n/4)*22.0;
 object c=PSpawn("cad_wiz",x,y);object t=PSpawn("nw_bandit001",x+1.5,y);
 SetTag(c,IntToString(n));SetImmortal(c,TRUE);SetImmortal(t,TRUE);SetPlotFlag(c,FALSE);SetPlotFlag(t,FALSE);SetBaseAttackBonus(1,c);
 ChangeToStandardFaction(c,STANDARD_FACTION_HOSTILE);ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);
 SetLocalObject(t,"caster",c);SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"cad_swing");
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDamageImmunityIncrease(DAMAGE_TYPE_BLUDGEONING,100),c);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDamageImmunityIncrease(DAMAGE_TYPE_PIERCING,100),c);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDamageImmunityIncrease(DAMAGE_TYPE_SLASHING,100),c);
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSkillIncrease(SKILL_CONCENTRATION,50),c);SetActionMode(c,ACTION_MODE_DEFENSIVE_CAST,TRUE);
 if(n==2||n==3)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);
 float start=8.0+IntToFloat(n)*0.15;DelayCommand(start,Begin(c,t,n));DelayCommand(start+20.0,Report(c));
 }DelayCommand(31.0,PDone());}
