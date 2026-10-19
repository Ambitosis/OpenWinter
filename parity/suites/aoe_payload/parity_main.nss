#include "parity_fx"
void Dump(effect e,string key) {
 PLog(key+".ints",PFxShort(e));PLogI(key+".level",GetEffectCasterLevel(e));PLogI(key+".spell",GetEffectSpellId(e));
}
void Read(object c,string key) {
 effect e=GetFirstEffect(c);while(GetIsEffectValid(e)) {if(GetEffectType(e)==EFFECT_TYPE_AREA_OF_EFFECT)Dump(e,key+".attached");e=GetNextEffect(c);}
}
void Probe(int index,int row,int level,int spell) {
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(10.0+IntToFloat(index)*8.0,10.0,0.0),0.0));
 int i;for(i=5000;i<=5012;i++)SetEventScript(c,i,"");
 SetLocalInt(c,"row",row);SetLocalString(c,"key","case"+IntToString(index));ExecuteScript("aop_creature",c);
 effect e=EffectAreaOfEffect(row,"aop_none","aop_none","aop_none");
 string key="case"+IntToString(index);Dump(e,key+".constructed");
 e=SetEffectCasterLevel(e,level);e=SetEffectSpellId(e,spell);Dump(e,key+".modified");
 ApplyEffectToObject(DURATION_TYPE_TEMPORARY,e,c,30.0);DelayCommand(0.5,Read(c,key));
}
void Cast(object c,int spell,int meta) {AssignCommand(c,ActionCastSpellAtObject(spell,c,meta,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,CLASS_TYPE_CLERIC));}
void Actual(int index,int spell,int meta=METAMAGIC_NONE) {
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_drowcler020",Location(GetObjectByTag("PARITY_AREA"),Vector(15.0+IntToFloat(index)*16.0,50.0,0.0),0.0));
 int i;for(i=5000;i<=5012;i++)SetEventScript(c,i,"");SetAILevel(c,AI_LEVEL_NORMAL);
 int level=GetSpellLevelByClass(CLASS_TYPE_CLERIC,spell)+(meta==METAMAGIC_EXTEND?1:0);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_EXTEND_SPELL),c);SetMemorizedSpell(c,CLASS_TYPE_CLERIC,level,0,spell,TRUE,meta);
 DelayCommand(9.0,Cast(c,spell,meta));
}
void main(){SetLocalInt(GetObjectByTag("PARITY_AREA"),"X2_L_WILD_MAGIC",TRUE);SetLocalString(GetModule(),"X2_S_UD_SPELLSCRIPT","aop_hook");Actual(0,SPELL_BATTLETIDE);Actual(1,SPELL_BLESS);Actual(2,SPELL_HEAL);Actual(3,SPELL_BLESS,METAMAGIC_EXTEND);Probe(0,0,0,-1);Probe(1,41,7,517);Probe(2,30,20,163);Probe(3,37,1,6);DelayCommand(18.0,PDone());}
