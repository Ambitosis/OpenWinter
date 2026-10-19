#include "parity_inc"
void Report(string key,object c,int level,int cls){PLogI(key+".after",GetMemorizedSpellReady(c,cls,level,0));}
void Cast(object c,object t,int spell,int meta,int cls){AssignCommand(c,ActionCastSpellAtObject(spell,t,meta,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,cls));}
void Case(int index,int spell,int meta,int level,int cls=CLASS_TYPE_WIZARD){
 string key=IntToString(index);object area=GetObjectByTag("PARITY_AREA");float x=10.0+IntToFloat(index%5)*25.0;float y=10.0+IntToFloat(index/5)*25.0;
 string blueprint="nw_drowmage020";if(cls==CLASS_TYPE_CLERIC)blueprint="nw_drowcler020";
 object c=CreateObject(OBJECT_TYPE_CREATURE,blueprint,Location(area,Vector(x,y,0.0),0.0));object t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(area,Vector(x+2.0,y,0.0),0.0));
 int e;for(e=5000;e<=5012;e++){SetEventScript(c,e,"");SetEventScript(t,e,"");}SetAILevel(c,AI_LEVEL_NORMAL);SetAILevel(t,AI_LEVEL_NORMAL);SetIsTemporaryEnemy(c,t);SetIsTemporaryEnemy(t,c);
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_MAXIMIZE_SPELL),c);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_EXTEND_SPELL),c);
 int l;int n;for(l=0;l<=9;l++)for(n=0;n<GetMemorizedSpellCountByLevel(c,cls,l);n++)SetMemorizedSpellReady(c,cls,l,n,FALSE);
 SetMemorizedSpell(c,cls,level,0,spell,TRUE,meta);
 PLogI(key+".level",GetSpellLevelByClass(cls,spell));PLogI(key+".stored",GetMemorizedSpellId(c,cls,level,0));PLogI(key+".meta",GetMemorizedSpellMetaMagic(c,cls,level,0));PLogI(key+".ready",GetMemorizedSpellReady(c,cls,level,0));
 DelayCommand(8.0,Cast(c,t,spell,meta,cls));DelayCommand(16.0,Report(key,c,level,cls));
}
void main(){
 Case(0,155,0,1);Case(1,155,0,2);Case(2,107,0,1);Case(3,107,0,2);Case(4,107,0,0);
 Case(5,102,METAMAGIC_EXTEND,2);Case(6,102,METAMAGIC_EXTEND,1);Case(7,102,METAMAGIC_EXTEND,3);
 Case(8,107,METAMAGIC_MAXIMIZE,4);Case(9,107,METAMAGIC_MAXIMIZE,3);Case(10,107,METAMAGIC_MAXIMIZE,5);Case(11,107,METAMAGIC_MAXIMIZE,1);Case(12,102,0,2);
 Case(13,102,0,1,CLASS_TYPE_CLERIC);Case(14,6,0,1,CLASS_TYPE_CLERIC);Case(15,6,0,2,CLASS_TYPE_CLERIC);
 DelayCommand(17.0,PDone());}
