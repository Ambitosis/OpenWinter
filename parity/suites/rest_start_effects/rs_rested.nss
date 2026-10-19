#include "rs_inc"
void main()
{
    object c=OBJECT_SELF;SetLocalInt(c,"rested",GetLocalInt(c,"rested")+1);
    RState(c,"rested_callback");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,TagEffect(EffectSkillIncrease(SKILL_SPOT,1),"hook"),c,300.0);
}
