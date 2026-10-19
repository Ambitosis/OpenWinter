// Real prepared control spells: observe natural expiry and restored statistics.
#include "parity_fx"

void Quiet(object actor)
{
    int eventId;
    for(eventId=5000;eventId<=5012;eventId++)SetEventScript(actor,eventId,"");
    SetAILevel(actor,AI_LEVEL_NORMAL);
    AssignCommand(actor,ClearAllActions(TRUE));
}
void Boundary(string key,object target,int spell,int after)
{
    PLogI(key+(after?".after.present":".before.present"),GetHasSpellEffect(spell,target));
    if(after)PLogI(key+".after.restored",PFxDiff(target)=="-");
}
void Observe(string key,int spell,int level,int meta)
{
    object chosen=OBJECT_INVALID;int consumed=0;int n;
    for(n=0;n<5;n++)
    {
        string member=key+"."+IntToString(n);
        object caster=GetLocalObject(GetModule(),member+"c");
        object target=GetLocalObject(GetModule(),member+"t");
        if(!GetMemorizedSpellReady(caster,CLASS_TYPE_WIZARD,level,0))consumed++;
        if(!GetIsObjectValid(chosen)&&GetHasSpellEffect(spell,target))chosen=target;
    }
    PLogI(key+".consumed",consumed);
    PLogI(key+".found",GetIsObjectValid(chosen));
    if(!GetIsObjectValid(chosen))return;
    effect e=GetFirstEffect(chosen);
    while(GetIsEffectValid(e)&&GetEffectSpellId(e)!=spell)e=GetNextEffect(chosen);
    PLogI(key+".duration",GetEffectDuration(e));
    PLogI(key+".durationType",GetEffectDurationType(e));
    PLog(key+".statChanges",PFxDiff(chosen));
    if(spell==4)
    {
        DelayCommand(230.0,Boundary(key,chosen,spell,TRUE));
        return;
    }
    float remaining=IntToFloat(GetEffectDurationRemaining(e));
    PLogI(key+".remaining.valid",remaining>1.0&&remaining<=IntToFloat(GetEffectDuration(e)));
    DelayCommand(remaining-0.25,Boundary(key,chosen,spell,FALSE));
    DelayCommand(remaining+1.25,Boundary(key,chosen,spell,TRUE));
}
void Cast(object caster,object target,int spell,int meta)
{
    AssignCommand(caster,ActionCastSpellAtObject(spell,target,meta,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,CLASS_TYPE_WIZARD));
}
void Case(int index,int spell,int level,int meta)
{
    string key=IntToString(index);int n;
    for(n=0;n<5;n++)
    {
        float x=10.0+IntToFloat(n)*30.0;float y=10.0+IntToFloat(index)*20.0;
        object area=GetObjectByTag("PARITY_AREA");
        object caster=CreateObject(OBJECT_TYPE_CREATURE,"nw_drowmage020",Location(area,Vector(x,y,0.0),0.0));
        object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit006",Location(area,Vector(x+2.0,y,0.0),0.0));
        Quiet(caster);Quiet(target);
        ChangeToStandardFaction(caster,STANDARD_FACTION_MERCHANT);ChangeToStandardFaction(target,STANDARD_FACTION_HOSTILE);
        SetIsTemporaryEnemy(caster,target);SetIsTemporaryEnemy(target,caster);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSavingThrowDecrease(SAVING_THROW_ALL,50),target);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_EXTEND_SPELL),caster);
        PFxSnap(target);
        int l;int slot;
        for(l=0;l<=9;l++)for(slot=0;slot<GetMemorizedSpellCountByLevel(caster,CLASS_TYPE_WIZARD,l);slot++)
            SetMemorizedSpellReady(caster,CLASS_TYPE_WIZARD,l,slot,FALSE);
        SetMemorizedSpell(caster,CLASS_TYPE_WIZARD,level,0,spell,TRUE,meta);
        string member=key+"."+IntToString(n);
        SetLocalObject(GetModule(),member+"c",caster);SetLocalObject(GetModule(),member+"t",target);
        DelayCommand(8.0,Cast(caster,target,spell,meta));
    }
    DelayCommand(16.0,Observe(key,spell,level,meta));
}
void main()
{
    Case(0,8,2,0);Case(1,82,5,0);Case(2,83,3,0);Case(3,166,3,0);
    Case(4,8,3,METAMAGIC_EXTEND);Case(5,82,6,METAMAGIC_EXTEND);
    Case(6,83,4,METAMAGIC_EXTEND);Case(7,166,4,METAMAGIC_EXTEND);
    Case(8,4,4,0);
    DelayCommand(255.0,PDone());
}
