// Run on a non-caster: cheat-cast Bull's Strength on itself, then try (and fail) a real cast.
void main()
{
    ActionCastSpellAtObject(SPELL_BULLS_STRENGTH, OBJECT_SELF, METAMAGIC_ANY, TRUE);
    ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, GetObjectByTag("spell_target"));
}
