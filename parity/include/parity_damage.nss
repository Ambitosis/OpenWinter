// One stock spell, independent living hostile targets, real spell slots.
#include "parity_inc"
void DQuiet(object o)
{
    int i;
    for (i=5000;i<=5012;i++) SetEventScript(o,i,"");
    SetAILevel(o,AI_LEVEL_NORMAL);
    AssignCommand(o,ClearAllActions(TRUE));
}
void DRead(object target,int sample)
{
    int damage=GetLocalInt(target,"before")-GetCurrentHitPoints(target);
    SetLocalInt(GetModule(),"damage"+IntToString(sample),damage);
    SetLocalInt(GetModule(),"samples",GetLocalInt(GetModule(),"samples")+1);
    object caster=GetLocalObject(target,"caster");
    if(!GetMemorizedSpellReady(caster,GetLocalInt(caster,"class"),GetLocalInt(caster,"slotlevel"),0))
        SetLocalInt(GetModule(),"consumed",GetLocalInt(GetModule(),"consumed")+1);
}
void DCast(object caster,object target,int spell)
{
    if(GetObjectSeen(target,caster)) SetLocalInt(GetModule(),"seen",GetLocalInt(GetModule(),"seen")+1);
    int cls=GetLocalInt(caster,"class");int lvl=GetLocalInt(caster,"slotlevel");
    if(GetMemorizedSpellId(caster,cls,lvl,0)==spell && GetMemorizedSpellReady(caster,cls,lvl,0)
        && GetMemorizedSpellMetaMagic(caster,cls,lvl,0)==GetLocalInt(caster,"meta"))
        SetLocalInt(GetModule(),"slots",GetLocalInt(GetModule(),"slots")+1);
    AssignCommand(caster,ActionCastSpellAtObject(spell,target,GetLocalInt(caster,"meta"),FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,cls));
}
void DSummary(int spell)
{
    object m=GetModule();
    int i;int j;int n=GetLocalInt(m,"count");
    for(i=0;i<n;i++) for(j=i+1;j<n;j++)
    {
        int a=GetLocalInt(m,"damage"+IntToString(i));
        int b=GetLocalInt(m,"damage"+IntToString(j));
        if(a>b) {SetLocalInt(m,"damage"+IntToString(i),b);SetLocalInt(m,"damage"+IntToString(j),a);}
    }
    string k="spell."+IntToString(spell);
    string expected=".expected"+IntToString(n);
    PLogI(k+".samples"+expected,GetLocalInt(m,"samples"));
    PLogI(k+".targets"+expected,GetLocalInt(m,"targets"));
    PLogI(k+".seen"+expected,GetLocalInt(m,"seen"));
    PLogI(k+".slots"+expected,GetLocalInt(m,"slots"));
    PLogI(k+".consumed"+expected,GetLocalInt(m,"consumed"));
    int peak=GetLocalInt(m,"damage"+IntToString(n-1));
    PLogI(k+".peak",peak);
    PLogI(k+".damage.expected1",peak>0);
    // Retain every observed amount as diagnostic output, without comparing random save/attack rolls.
    string values="";
    for(i=0;i<n;i++) values+=IntToString(GetLocalInt(m,"damage"+IntToString(i)))+",";
    PrintString("DAMAGE_SAMPLES|"+IntToString(spell)+"|"+values);
    PDone();
}
void DCase(int spell,string casterBlueprint,int cls,int meta=METAMAGIC_MAXIMIZE,int count=11)
{
    object area=GetObjectByTag("PARITY_AREA");
    int i;
    SetLocalInt(GetModule(),"count",count);
    for(i=0;i<count;i++)
    {
        float x=15.0+IntToFloat(i%4)*40.0;
        float y=15.0+IntToFloat(i/4)*40.0;
        object caster=CreateObject(OBJECT_TYPE_CREATURE,casterBlueprint,Location(area,Vector(x,y,0.0),0.0));
        object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(area,Vector(x+1.5,y,0.0),180.0));
        DQuiet(caster);DQuiet(target);
        int adjustment=0;if(meta==METAMAGIC_MAXIMIZE)adjustment=3;
        int slotlevel=GetSpellLevelByClass(cls,spell)+adjustment;
        SetMemorizedSpell(caster,cls,slotlevel,0,spell,TRUE,meta);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_MAXIMIZE_SPELL),caster);
        SetLocalInt(caster,"class",cls);SetLocalInt(caster,"slotlevel",slotlevel);SetLocalInt(caster,"meta",meta);
        SetLocalObject(target,"caster",caster);
        ChangeToStandardFaction(caster,STANDARD_FACTION_DEFENDER);
        ChangeToStandardFaction(target,STANDARD_FACTION_HOSTILE);
        SetIsTemporaryEnemy(caster,target);SetIsTemporaryEnemy(target,caster);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectTemporaryHitpoints(10000),target);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectTemporaryHitpoints(10000),caster);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSavingThrowDecrease(SAVING_THROW_ALL,100),target);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectACDecrease(50),target);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectAttackIncrease(50),caster);
        SetLocalInt(target,"before",GetCurrentHitPoints(target));
        if(GetIsObjectValid(target) && GetIsEnemy(target,caster))
            SetLocalInt(GetModule(),"targets",GetLocalInt(GetModule(),"targets")+1);
        // NPC perception is refreshed on a longer schedule than the PC visual fixtures.
        DelayCommand(9.0,DCast(caster,target,spell));
        DelayCommand(14.0,DRead(target,i));
    }
    DelayCommand(14.5,DSummary(spell));
}
