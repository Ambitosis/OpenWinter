#include "parity_inc"
void Read(object caster,int spell,int level,int seconds)
{
    string key="spell."+IntToString(spell)+".t"+IntToString(seconds);
    PLogI(key+".hp",GetCurrentHitPoints(caster));
    PLogI(key+".consumed",!GetMemorizedSpellReady(caster,CLASS_TYPE_CLERIC,level,0));
    PLogI(key+".hasEffect",GetHasSpellEffect(spell,caster));
}
void Cast(object caster,int spell,int meta,int level)
{
    SetCurrentHitPoints(caster,1);
    string key="spell."+IntToString(spell);
    PLogI(key+".before",GetCurrentHitPoints(caster));
    PLogI(key+".max",GetMaxHitPoints(caster));
    PLogI(key+".prepared",GetMemorizedSpellId(caster,CLASS_TYPE_CLERIC,level,0)==spell
        && GetMemorizedSpellReady(caster,CLASS_TYPE_CLERIC,level,0)
        && GetMemorizedSpellMetaMagic(caster,CLASS_TYPE_CLERIC,level,0)==meta);
    AssignCommand(caster,ActionCastSpellAtObject(spell,caster,meta,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,CLASS_TYPE_CLERIC));
    DelayCommand(2.0,Read(caster,spell,level,2));
    DelayCommand(8.0,Read(caster,spell,level,8));
    DelayCommand(14.0,Read(caster,spell,level,14));
}
void Case(int index,int spell,int meta)
{
    float x=15.0+IntToFloat(index%4)*40.0;float y=15.0+IntToFloat(index/4)*40.0;
    object caster=CreateObject(OBJECT_TYPE_CREATURE,"nw_drowcler020",Location(GetObjectByTag("PARITY_AREA"),Vector(x,y,0.0),0.0));
    int i;for(i=5000;i<=5012;i++)SetEventScript(caster,i,"");SetAILevel(caster,AI_LEVEL_NORMAL);
    int level=GetSpellLevelByClass(CLASS_TYPE_CLERIC,spell);if(meta==METAMAGIC_MAXIMIZE)level+=3;
    SetMemorizedSpell(caster,CLASS_TYPE_CLERIC,level,0,spell,TRUE,meta);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_MAXIMIZE_SPELL),caster);
    DelayCommand(0.5,Cast(caster,spell,meta,level));
}
void main()
{
    Case(0,SPELL_CURE_MINOR_WOUNDS,METAMAGIC_NONE);
    Case(1,SPELL_CURE_LIGHT_WOUNDS,METAMAGIC_MAXIMIZE);
    Case(2,SPELL_CURE_MODERATE_WOUNDS,METAMAGIC_MAXIMIZE);
    Case(3,SPELL_CURE_SERIOUS_WOUNDS,METAMAGIC_MAXIMIZE);
    Case(4,SPELL_CURE_CRITICAL_WOUNDS,METAMAGIC_MAXIMIZE);
    Case(5,SPELL_HEAL,METAMAGIC_NONE);
    Case(6,SPELL_MASS_HEAL,METAMAGIC_NONE);
    Case(7,SPELL_HEALING_CIRCLE,METAMAGIC_MAXIMIZE);
    Case(8,SPELL_REGENERATE,METAMAGIC_NONE);
    Case(9,SPELL_MONSTROUS_REGENERATION,METAMAGIC_NONE);
    DelayCommand(16.0,PDone());
}
