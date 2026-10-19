// Application/stat checks; spell-slot legality and save distributions are separate suites.
#include "parity_fx"
int BRandom(int spell) { return spell==1 || spell==9 || spell==13 || spell==49 || spell==73 || spell==99 || spell==169 || (spell>=354 && spell<=362) || spell==441; }
// Normalize only the actual random integer; retain every other effect field.
string BInts(effect e,int spell) {
 int type=GetEffectType(e,TRUE);
 if(type==EFFECT_TYPE_AREA_OF_EFFECT) return IntToString(GetEffectInteger(e,0)); // remaining integers are native AoE runtime state
 int index=-1;
 if(BRandom(spell)) {
  if(type==EFFECT_TYPE_ABILITY_INCREASE || type==EFFECT_TYPE_SPELLLEVELABSORPTION) index=1;
  if(type==EFFECT_TYPE_TEMPORARY_HITPOINTS || type==EFFECT_TYPE_ELEMENTALSHIELD) index=0;
 }
 if(index<0) return PFxInts(e);
 string s=""; int i;
 for(i=0;i<8;i++) { if(i>0)s+=","; s+=i==index?"r":IntToString(GetEffectInteger(e,i)); }
 return s;
}
// Installed authored scripts define these ranges. Ability/HP bonuses must also change real stats.
int BRollValid(object o,int spell) {
 int found=FALSE; int valid=TRUE; effect e=GetFirstEffect(o);
 while(GetIsEffectValid(e)) {
  int type=GetEffectType(e,TRUE); int n=0; int low=0; int high=0;
  if(type==EFFECT_TYPE_ABILITY_INCREASE) {
   int ability=GetEffectInteger(e,0); n=GetEffectInteger(e,1);
   low=2;high=5;if(spell>=357 && spell<=362) {low=3;high=9;}
   if(GetAbilityScore(o,ability)-GetLocalInt(o,"pfx_snap"+IntToString(4+ability))!=n)valid=FALSE;
  } else if(type==EFFECT_TYPE_TEMPORARY_HITPOINTS) {
   n=GetEffectInteger(e,0);low=1;high=8;
   if(GetCurrentHitPoints(o)-GetLocalInt(o,"pfx_snap16")!=n)valid=FALSE;
  } else if(type==EFFECT_TYPE_SPELLLEVELABSORPTION) {
   n=GetEffectInteger(e,1);low=7;high=10;
   if(spell==73) {low=11;high=22;} else if(spell==169) {low=9;high=16;}
  } else if(type==EFFECT_TYPE_ELEMENTALSHIELD) {
   n=GetEffectInteger(e,0);low=GetEffectCasterLevel(e)+1;high=low+5;
  }
  if(high>0) {found=TRUE;if(n<low || n>high)valid=FALSE;}
  e=GetNextEffect(o);
 }
 return found && valid;
}
string BDetail(object o,int spell) {
 string s=""; effect e=GetFirstEffect(o);
 while(GetIsEffectValid(e)) {
  if(GetEffectType(e,TRUE)==EFFECT_TYPE_VISUALEFFECT) { e=GetNextEffect(o); continue; } // presentation timing is measured in visual fixtures
  if(s!="") s+=" ";
  s+=IntToString(GetEffectType(e,TRUE))+":"+IntToString(GetEffectSubType(e))+":"+IntToString(GetEffectDurationType(e))
   +":"+IntToString(GetEffectSpellId(e))+"["+BInts(e,spell)+"]d"+IntToString(GetEffectDuration(e))+"c"+PFxCreator(e)+"l"+IntToString(GetEffectCasterLevel(e));
  e=GetNextEffect(o);
 }
 return s==""?"-":s;
}
void BReport(object o,int spell) {
 string k="spell."+IntToString(spell);
 if(BRandom(spell)) PLogI(k+".changed",PFxDiff(o)!="-" || BDetail(o,spell)!="-"); else PLog(k+".delta",PFxDiff(o));
 if(BRandom(spell)) PLogI(k+".roll.valid.expected1",BRollValid(o,spell));
 PLog(k+".effects",BDetail(o,spell)); PLogI(k+".has",GetHasSpellEffect(spell,o));
}
void BCast(object o,int spell) { AssignCommand(o,ActionCastSpellAtObject(spell,o,METAMAGIC_NONE,TRUE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE)); }
void BCase(int cell,int spell,string blueprint) {
 float x=15.0+IntToFloat(cell%4)*30.0; float y=15.0+IntToFloat(cell/4)*30.0;
 object o=CreateObject(OBJECT_TYPE_CREATURE,blueprint,Location(GetObjectByTag("PARITY_AREA"),Vector(x,y,0.0),0.0),FALSE,"caster");
 int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,""); SetAILevel(o,AI_LEVEL_NORMAL);
 PFxSnap(o); DelayCommand(1.0,BCast(o,spell)); DelayCommand(5.0,BReport(o,spell));
}
