// Run on the caster: queue Magic Missile at the target.
void main()
{
    object oTarget = GetObjectByTag("spell_target");
    ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oTarget);
    SetLocalInt(GetModule(), "mm_action", GetCurrentAction());
}
