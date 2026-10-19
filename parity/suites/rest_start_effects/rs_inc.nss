#include "parity_inc"
int RHas(object c,string tag)
{
    effect e=GetFirstEffect(c);
    while(GetIsEffectValid(e))
    {
        if(GetEffectTag(e)==tag)return TRUE;
        e=GetNextEffect(c);
    }
    return FALSE;
}
void RState(object c,string stage)
{
    string k=GetTag(c)+"."+stage;
    PLogI(k+".valid",GetIsObjectValid(c));PLogI(k+".pc",GetIsPC(c));
    PLogI(k+".resting",GetIsResting(c));PLogI(k+".action",GetCurrentAction(c));
    PLogI(k+".rested",GetLocalInt(c,"rested"));PLogI(k+".finished",GetLocalInt(c,"finished"));
    PLogI(k+".tm",RHas(c,"tm"));PLogI(k+".ts",RHas(c,"ts"));PLogI(k+".te",RHas(c,"te"));PLogI(k+".tu",RHas(c,"tu"));
    PLogI(k+".pm",RHas(c,"pm"));PLogI(k+".ps",RHas(c,"ps"));PLogI(k+".pe",RHas(c,"pe"));PLogI(k+".pu",RHas(c,"pu"));
    PLogI(k+".late",RHas(c,"late"));PLogI(k+".hook",RHas(c,"hook"));
}
void RKit(object c)
{
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,TagEffect(EffectSkillIncrease(SKILL_SPOT,1),"tm"),c,300.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,TagEffect(SupernaturalEffect(EffectSkillIncrease(SKILL_SPOT,1)),"ts"),c,300.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,TagEffect(ExtraordinaryEffect(EffectSkillIncrease(SKILL_SPOT,1)),"te"),c,300.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,TagEffect(UnyieldingEffect(EffectSkillIncrease(SKILL_SPOT,1)),"tu"),c,300.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,TagEffect(EffectSkillIncrease(SKILL_SPOT,1),"pm"),c);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,TagEffect(SupernaturalEffect(EffectSkillIncrease(SKILL_SPOT,1)),"ps"),c);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,TagEffect(ExtraordinaryEffect(EffectSkillIncrease(SKILL_SPOT,1)),"pe"),c);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,TagEffect(UnyieldingEffect(EffectSkillIncrease(SKILL_SPOT,1)),"pu"),c);
}
