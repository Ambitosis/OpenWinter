#include "parity_inc"
string Effects(object o){string result="";effect e=GetFirstEffect(o);int limit=0;while(GetIsEffectValid(e)&&limit<40){result+=IntToString(GetEffectType(e,TRUE))+":"+IntToString(GetEffectSpellId(e))+":"+IntToString(GetEffectDuration(e))+" ";e=GetNextEffect(o);limit++;}return result;}
void Report(string key,object caster,int type,int spell,int level) {
 object s=GetAssociate(type,caster);PLogI(key+".valid",GetIsObjectValid(s));
 PLogI(key+".second",GetIsObjectValid(GetAssociate(type,caster,2)));
 if(type==ASSOCIATE_TYPE_SUMMONED){PLogI(key+".ready0",GetMemorizedSpellReady(caster,CLASS_TYPE_CLERIC,level,0));PLogI(key+".ready1",GetMemorizedSpellReady(caster,CLASS_TYPE_CLERIC,level,1));}
 else PLogI(key+".uses",GetFeatRemainingUses(spell,caster));
 if(!GetIsObjectValid(s))return;
 PLogI(key+".master",GetMaster(s)==caster);PLogI(key+".type",GetAssociateType(s));
 PLogI(key+".faction",GetFactionEqual(s,caster));
 string res=GetResRef(s);WriteTimestampedLogEntry("SUMMON|"+key+"|resref="+res+"|name="+GetName(s));
 if(level<6||type!=ASSOCIATE_TYPE_SUMMONED)PLog(key+".resref",res);
 else {
  string suffix="huge";if(level==7)suffix="great";if(level>=8)suffix="elder";
  PLogI(key+".choice",res=="nw_s_air"+suffix||res=="nw_s_water"+suffix||res=="nw_s_fire"+suffix);
 }
 object control=CreateObject(OBJECT_TYPE_CREATURE,res,PCentre(-10.0,-10.0));
 PLogI(key+".hd_delta",GetHitDice(s)-GetHitDice(control));
 PLogI(key+".hp_delta",GetMaxHitPoints(s)-GetMaxHitPoints(control));
 PLogI(key+".ac_delta",GetAC(s)-GetAC(control));
 PLogI(key+".appearance_delta",GetAppearanceType(s)-GetAppearanceType(control));
 int a;for(a=0;a<6;a++)PLogI(key+".ability"+IntToString(a)+"_delta",GetAbilityScore(s,a)-GetAbilityScore(control,a));
 PLog(key+".effects",Effects(s));PLog(key+".mastereffects",Effects(caster));
 DestroyObject(control);
}
void Cast(object c,int spell){location loc=GetLocation(c);AssignCommand(c,ActionCastSpellAtLocation(spell,loc,METAMAGIC_NONE,FALSE,PROJECTILE_PATH_TYPE_DEFAULT,FALSE,CLASS_TYPE_CLERIC));}
void Case(int index,int spell,int level){
 object area=GetObjectByTag("PARITY_AREA");vector pos=Vector(20.0+IntToFloat(index%4)*30.0,20.0+IntToFloat(index/4)*30.0,0.0);
 object c=CreateObject(OBJECT_TYPE_CREATURE,"sum_animal",Location(area,pos,0.0),FALSE,"SUM_"+IntToString(level));SetAILevel(c,AI_LEVEL_NORMAL);
 string key="spell"+IntToString(level);PLogI(key+".domain1",GetDomain(c,1));PLogI(key+".domain2",GetDomain(c,2));PLogI(key+".animalfeat",GetHasFeat(FEAT_ANIMAL_DOMAIN_POWER,c));
 PLogI(key+".known",GetIsInKnownSpellList(c,CLASS_TYPE_CLERIC,spell));PLogI(key+".ready",GetMemorizedSpellReady(c,CLASS_TYPE_CLERIC,level,0));
 DelayCommand(2.0,Cast(c,spell));DelayCommand(10.0,Report(key,c,ASSOCIATE_TYPE_SUMMONED,spell,level));
}
void main(){Case(0,SPELL_SUMMON_CREATURE_I,1);Case(1,SPELL_SUMMON_CREATURE_II,2);Case(2,SPELL_SUMMON_CREATURE_III,3);Case(3,SPELL_SUMMON_CREATURE_IV,4);Case(4,SPELL_SUMMON_CREATURE_V,5);Case(5,SPELL_SUMMON_CREATURE_VI,6);Case(6,SPELL_SUMMON_CREATURE_VII,7);Case(7,SPELL_SUMMON_CREATURE_VIII,8);Case(8,SPELL_SUMMON_CREATURE_IX,9);DelayCommand(12.0,PDone());}
