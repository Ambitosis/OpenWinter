// Run on a cleric: spontaneous Cure Light Wounds on itself.
void main()
{
    ActionCastSpellAtObject(SPELL_CURE_LIGHT_WOUNDS, OBJECT_SELF, METAMAGIC_ANY, FALSE, 0,
        PROJECTILE_PATH_TYPE_DEFAULT, FALSE, CLASS_TYPE_CLERIC, TRUE);
}
