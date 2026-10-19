#include "parity_inc"
#include "parity_time"
int HasType(object o,int type)
{
    effect e=GetFirstEffect(o);
    while(GetIsEffectValid(e))
    {
        if(GetEffectType(e)==type)return TRUE;
        e=GetNextEffect(o);
    }
    return FALSE;
}
void Quiet(object o)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE));SetAILevel(o,AI_LEVEL_NORMAL);
}
void Finished(object a,object t)
{
    PLogF("finish",PNow()-GetLocalFloat(a,"start"),0.2);
    // Drain magnitude depends on the stock touch/save rolls, not dispatch parity.
    WriteTimestampedLogEntry("Extract Brain intelligence after: "+IntToString(GetAbilityScore(t,ABILITY_INTELLIGENCE)));
}
void Go(object a,object t)
{
    PLogI("enemy",GetIsEnemy(t,a));PLogI("seen",GetObjectSeen(t,a));
    PLogI("race",GetRacialType(t));PLogI("paralyzed",HasType(t,EFFECT_TYPE_PARALYZE));
    PLogI("polymorphed",HasType(t,EFFECT_TYPE_POLYMORPH));PLogI("petrified",HasType(t,EFFECT_TYPE_PETRIFY));
    PLogF("distance",GetDistanceBetween(a,t),0.01);
    SetLocalFloat(a,"start",PNow());
    AssignCommand(a,ActionCastSpellAtObject(716,t,METAMAGIC_ANY,TRUE,0,PROJECTILE_PATH_TYPE_DEFAULT));
    AssignCommand(a,ActionDoCommand(Finished(a,t)));
}
void Setup(object a,object t)
{
    Quiet(a);Quiet(t);SetPlotFlag(t,FALSE);
    SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"eb_event");
    SetIsTemporaryEnemy(t,a);SetIsTemporaryEnemy(a,t);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectParalyze(),t,20.0);
}
void main()
{
    object a=CreateObject(OBJECT_TYPE_CREATURE,"x2_mindflayer001",PCentre(0.0,0.0));
    object t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(0.0,-1.5));
    SetLocalObject(t,"caster",a);
    DelayCommand(.3,Setup(a,t));DelayCommand(5.0,Go(a,t));DelayCommand(10.0,PDone());
}
