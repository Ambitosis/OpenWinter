// A spell whose effects carry its spell id: +2 to Strength, Hide and Fortitude.
void main()
{
    object o = GetSpellTargetObject();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 2), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_HIDE, 2), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowIncrease(SAVING_THROW_FORT, 2), o);
}
