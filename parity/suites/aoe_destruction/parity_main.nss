#include "parity_inc"
int Areas(object target)
{
    int count=0;effect e=GetFirstEffect(target);
    while(GetIsEffectValid(e)){if(GetEffectType(e,TRUE)==EFFECT_TYPE_AREA_OF_EFFECT)count++;e=GetNextEffect(target);}
    return count;
}
void Find(string key,object target,int destroy)
{
    object aoe=GetNearestObjectToLocation(OBJECT_TYPE_AREA_OF_EFFECT,GetLocation(target));
    SetLocalString(aoe,"case",key);SetLocalObject(GetModule(),key,aoe);
    PLogI(key+".created",GetIsObjectValid(aoe));
    if(destroy)DelayCommand(2.0,DestroyObject(aoe));
}
void Report(string key,object target)
{
    PLogI(key+".valid",GetIsObjectValid(GetLocalObject(GetModule(),key)));
    PLogI(key+".exits",GetLocalInt(GetModule(),key+".exits"));
    PLogI(key+".attachedEffects",Areas(target));
}
void Case(string key,float x,int attached,int destroy)
{
    location at=Location(GetObjectByTag("PARITY_AREA"),Vector(x,20.0,0.0),0.0);
    object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",at);
    int i;for(i=5000;i<=5012;i++)SetEventScript(target,i,"");SetAILevel(target,AI_LEVEL_NORMAL);
    effect e=EffectAreaOfEffect(AOE_PER_FOGACID,"","","aoed_exit");
    float duration=3.0;if(destroy)duration=20.0;
    if(attached)ApplyEffectToObject(DURATION_TYPE_TEMPORARY,e,target,duration);
    else ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,e,at,duration);
    DelayCommand(0.2,Find(key,target,destroy));
    DelayCommand(5.0,Report(key,target));
}
void main()
{
    Case("script",15.0,FALSE,TRUE);
    Case("expiry",45.0,FALSE,FALSE);
    Case("attached",75.0,TRUE,TRUE);
    DelayCommand(6.0,PDone());
}
