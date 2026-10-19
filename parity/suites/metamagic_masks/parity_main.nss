#include "parity_inc"
void Report(string key,object caster,int cls,int level)
{
    PLogI(key+".ready",GetMemorizedSpellReady(caster,cls,level,0));
}
void Cast(object caster,object target,int spell,int meta,int cls)
{
    AssignCommand(caster,ActionCastSpellAtObject(spell,target,meta,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,cls));
}
void Case(int index,string key,int spell,int meta,int cls)
{
    string blueprint="nw_drowmage020";if(cls==CLASS_TYPE_CLERIC)blueprint="nw_drowcler020";
    object area=GetObjectByTag("PARITY_AREA");float x=15.0+IntToFloat(index%4)*30.0;float y=15.0+IntToFloat(index/4)*30.0;
    object caster=CreateObject(OBJECT_TYPE_CREATURE,blueprint,Location(area,Vector(x,y,0.0),0.0));
    object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit006",Location(area,Vector(x+1.5,y,0.0),0.0));
    int i;for(i=5000;i<=5012;i++){SetEventScript(caster,i,"");SetEventScript(target,i,"");}
    SetAILevel(caster,AI_LEVEL_NORMAL);SetAILevel(target,AI_LEVEL_NORMAL);
    SetIsTemporaryEnemy(caster,target);SetIsTemporaryEnemy(target,caster);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_MAXIMIZE_SPELL),caster);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_EXTEND_SPELL),caster);
    int adjustment=0;if(meta==METAMAGIC_MAXIMIZE)adjustment=3;else if(meta==METAMAGIC_EXTEND)adjustment=1;
    int level=GetSpellLevelByClass(cls,spell)+adjustment;
    SetMemorizedSpell(caster,cls,level,0,spell,TRUE,meta);
    PLogI(key+".prepared",GetMemorizedSpellReady(caster,cls,level,0));
    DelayCommand(9.0,Cast(caster,target,spell,meta,cls));
    DelayCommand(14.0,Report(key,caster,cls,level));
}
void main()
{
    Case(0,"minor.normal",431,METAMAGIC_NONE,CLASS_TYPE_CLERIC);
    Case(1,"minor.maximize",431,METAMAGIC_MAXIMIZE,CLASS_TYPE_CLERIC);
    Case(2,"missile.normal",107,METAMAGIC_NONE,CLASS_TYPE_WIZARD);
    Case(3,"missile.maximize",107,METAMAGIC_MAXIMIZE,CLASS_TYPE_WIZARD);
    Case(4,"missile.extend",107,METAMAGIC_EXTEND,CLASS_TYPE_WIZARD);
    Case(5,"armor.normal",102,METAMAGIC_NONE,CLASS_TYPE_WIZARD);
    Case(6,"armor.maximize",102,METAMAGIC_MAXIMIZE,CLASS_TYPE_WIZARD);
    Case(7,"armor.extend",102,METAMAGIC_EXTEND,CLASS_TYPE_WIZARD);
    DelayCommand(15.0,PDone());
}
