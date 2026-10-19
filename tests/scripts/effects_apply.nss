// Effects package test: constructors, getters, linking, iteration and removal.
// OBJECT_SELF = the caster; target tagged "fx_target". Results go to module locals.
int CountEffects(object oT)
{
    int n = 0;
    effect e = GetFirstEffect(oT);
    while (GetIsEffectValid(e))
    {
        n++;
        e = GetNextEffect(oT);
    }
    return n;
}

void main()
{
    object oM = GetModule();
    object oT = GetObjectByTag("fx_target");

    effect eHaste = EffectHaste();
    SetLocalInt(oM, "haste_type", GetEffectType(eHaste));
    SetLocalInt(oM, "heal_hidden", GetEffectType(EffectHeal(1)));
    SetLocalInt(oM, "heal_all", GetEffectType(EffectHeal(1), TRUE));
    SetLocalInt(oM, "heal_invalid", GetIsEffectValid(EffectHeal(-1)));
    SetLocalInt(oM, "default_subtype", GetEffectSubType(eHaste));
    SetLocalInt(oM, "default_spellid", GetEffectSpellId(eHaste));

    effect eAC = EffectACIncrease(3, AC_DEFLECTION_BONUS);
    SetLocalInt(oM, "ac_int0", GetEffectInteger(eAC, 0));
    SetLocalInt(oM, "ac_int1", GetEffectInteger(eAC, 1));
    SetLocalInt(oM, "ac_int2", GetEffectInteger(eAC, 2));
    SetLocalFloat(oM, "vfx_scale", GetEffectFloat(EffectVisualEffect(VFX_DUR_GLOW_BLUE, FALSE, 2.5), 0));

    // A modifier returns a modified copy and leaves the original alone.
    effect eTagged = TagEffect(eAC, "copy");
    SetLocalString(oM, "orig_tag", GetEffectTag(eAC));
    SetLocalString(oM, "copy_tag", GetEffectTag(eTagged));

    effect eLink = EffectLinkEffects(EffectVisualEffect(VFX_DUR_GLOW_BLUE), eAC);
    eLink = SupernaturalEffect(eLink);
    eLink = TagEffect(eLink, "linked");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, eLink, oT, 10.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, eHaste, oT);

    int nCount = 0;
    int nSuper = 0;
    int bSameLink = TRUE;
    int nRemaining = -1;
    int bCreator = FALSE;
    string sLink = "";
    effect e = GetFirstEffect(oT);
    while (GetIsEffectValid(e))
    {
        nCount++;
        if (GetEffectSubType(e) == SUBTYPE_SUPERNATURAL) nSuper++;
        if (GetEffectTag(e) == "linked")
        {
            if (sLink == "") sLink = GetEffectLinkId(e);
            else if (sLink != GetEffectLinkId(e)) bSameLink = FALSE;
            nRemaining = GetEffectDurationRemaining(e);
            SetLocalInt(oM, "link_duration", GetEffectDuration(e));
            SetLocalInt(oM, "link_durtype", GetEffectDurationType(e));
        }
        if (GetEffectCreator(e) == OBJECT_SELF) bCreator = TRUE;
        e = GetNextEffect(oT);
    }
    SetLocalInt(oM, "count", nCount);
    SetLocalInt(oM, "super", nSuper);
    SetLocalInt(oM, "same_link", bSameLink);
    SetLocalInt(oM, "remaining", nRemaining);
    SetLocalInt(oM, "creator_self", bCreator);

    // Removing one effect of a link removes the whole link.
    e = GetFirstEffect(oT);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_AC_INCREASE) RemoveEffect(oT, e);
        e = GetNextEffect(oT);
    }
    SetLocalInt(oM, "after_remove", CountEffects(oT));

    // Spell id and GetHasSpellEffect.
    effect eSpell = SetEffectSpellId(EffectAttackIncrease(2), 123);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, eSpell, oT);
    SetLocalInt(oM, "has_spell", GetHasSpellEffect(123, oT));
    SetLocalInt(oM, "has_other_spell", GetHasSpellEffect(124, oT));

    // Immunity: a target immune to paralysis rejects the paralysis and its linked visual.
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_PARALYSIS), oT);
    int nBefore = CountEffects(oT);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectLinkEffects(EffectVisualEffect(VFX_DUR_GLOW_BLUE), EffectParalyze()), oT);
    SetLocalInt(oM, "immune_added", CountEffects(oT) - nBefore);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, IgnoreEffectImmunity(EffectParalyze()), oT);
    SetLocalInt(oM, "ignore_added", CountEffects(oT) - nBefore);
}
