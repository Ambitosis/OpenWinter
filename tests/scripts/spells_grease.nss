// Run on the caster: cheat-cast Grease at the target's feet.
void main()
{
    ActionCastSpellAtLocation(SPELL_GREASE, GetLocation(GetObjectByTag("spell_target")), METAMAGIC_ANY, TRUE);
}
