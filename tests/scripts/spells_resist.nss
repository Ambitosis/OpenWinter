// Run on the caster (its current spell is Magic Missile): resistance, immunity and absorption.
void main()
{
    object oM = GetModule();
    object oSR = GetObjectByTag("spell_sr");
    object oImm = GetObjectByTag("spell_imm");
    object oMantle = GetObjectByTag("spell_mantle");
    object oGlobe = GetObjectByTag("spell_globe");
    object oPlain = GetObjectByTag("spell_plain");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellResistanceIncrease(100), oSR);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellImmunity(SPELL_MAGIC_MISSILE), oImm);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellLevelAbsorption(9, 10), oMantle);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellLevelAbsorption(4), oGlobe);

    SetLocalInt(oM, "r_sr", ResistSpell(OBJECT_SELF, oSR));
    SetLocalInt(oM, "r_imm", ResistSpell(OBJECT_SELF, oImm));
    SetLocalInt(oM, "r_mantle", ResistSpell(OBJECT_SELF, oMantle));
    SetLocalInt(oM, "r_globe", ResistSpell(OBJECT_SELF, oGlobe));
    SetLocalInt(oM, "r_plain", ResistSpell(OBJECT_SELF, oPlain));
    SetLocalInt(oM, "r_invalid", ResistSpell(OBJECT_SELF, OBJECT_INVALID));
    effect e = GetFirstEffect(oMantle);
    SetLocalInt(oM, "r_mantle_left", GetEffectInteger(e, 1));

    SetLocalInt(oM, "c_sr", SpellResistanceCheck(oSR, OBJECT_SELF));
    SetLocalInt(oM, "c_sr_plain", SpellResistanceCheck(oPlain, OBJECT_SELF));
    SetLocalInt(oM, "c_imm", SpellImmunityCheck(oImm, OBJECT_SELF));
    SetLocalInt(oM, "c_imm_other", SpellImmunityCheck(oImm, OBJECT_SELF, SPELL_FIREBALL));
    SetLocalInt(oM, "c_globe", SpellAbsorptionUnlimitedCheck(oGlobe, OBJECT_SELF));
    SetLocalInt(oM, "c_globe_high", SpellAbsorptionUnlimitedCheck(oGlobe, OBJECT_SELF, SPELL_FIREBALL, -1, 5));
    SetLocalInt(oM, "c_mantle_keep", SpellAbsorptionLimitedCheck(oMantle, OBJECT_SELF, -1, -1, -1, FALSE));
    SetLocalInt(oM, "c_mantle_left", GetEffectInteger(GetFirstEffect(oMantle), 1));

    SetLocalInt(oM, "reflex", GetReflexAdjustedDamage(20, oPlain, 1));
}
