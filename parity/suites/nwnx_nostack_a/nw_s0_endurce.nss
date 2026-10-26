// A spell whose effects carry its spell id: +1 to Strength, Hide and Fortitude.
void main()
{
    object o = GetSpellTargetObject();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 1), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_HIDE, 1), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowIncrease(SAVING_THROW_FORT, 1), o);
}
