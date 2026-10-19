// Neighbour OnDamaged: heals what it took.
void main()
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectHeal(GetTotalDamageDealt()), OBJECT_SELF);
}
