// OnSpellCastAt recorder for SpellTests.
void main()
{
    object oM = GetModule();
    SetLocalInt(oM, "sat_count", GetLocalInt(oM, "sat_count") + 1);
    SetLocalInt(oM, "sat_spell", GetLastSpell());
    SetLocalObject(oM, "sat_caster", GetLastSpellCaster());
    SetLocalInt(oM, "sat_harmful", GetLastSpellHarmful());
}
