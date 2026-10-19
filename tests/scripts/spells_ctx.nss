// Run on the caster after a spell: the spell context getters.
void main()
{
    object oM = GetModule();
    SetLocalInt(oM, "ctx_spell", GetSpellId());
    SetLocalObject(oM, "ctx_target", GetSpellTargetObject());
    SetLocalLocation(oM, "ctx_loc", GetSpellTargetLocation());
    SetLocalInt(oM, "ctx_class", GetLastSpellCastClass());
    SetLocalInt(oM, "ctx_level", GetLastSpellLevel());
    SetLocalInt(oM, "ctx_spont", GetSpellCastSpontaneously());
    SetLocalInt(oM, "ctx_cl", GetCasterLevel(OBJECT_SELF));
    SetLocalInt(oM, "ctx_meta", GetMetaMagicFeat());
    SetLocalInt(oM, "ctx_dc", GetSpellSaveDC());
    SetLocalObject(oM, "ctx_item", GetSpellCastItem());
}
