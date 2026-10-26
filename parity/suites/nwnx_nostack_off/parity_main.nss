// NWNX_NoStack: how Strength, Hide and Fortitude bonuses (and penalties) from three spells, script effects and two
// rings add up under each stacking mode. The nwnx_nostack_* suites run this script with different NWNX_NOSTACK_*
// settings; this one with none.
#include "parity_inc"
#include "nwnx_nostack"

object Make(string sTag, float fX)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(fX));
    SetTag(o, sTag);
    int e;
    for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetLocalInt(o, "str", GetAbilityScore(o, ABILITY_STRENGTH));
    SetLocalInt(o, "hide", GetSkillRank(SKILL_HIDE, o));
    SetLocalInt(o, "fort", GetFortitudeSavingThrow(o));
    return o;
}

void Ring(object o, int nSlot, itemproperty ip1, itemproperty ip2, itemproperty ip3)
{
    object r = CreateItemOnObject("nw_it_mring021", o);
    itemproperty ip = GetFirstItemProperty(r);
    while (GetIsItemPropertyValid(ip)) { RemoveItemProperty(r, ip); ip = GetFirstItemProperty(r); }
    AddItemProperty(DURATION_TYPE_PERMANENT, ip1, r);
    AddItemProperty(DURATION_TYPE_PERMANENT, ip2, r);
    AddItemProperty(DURATION_TYPE_PERMANENT, ip3, r);
    SetIdentified(r, TRUE);
    AssignCommand(o, ActionEquipItem(r, nSlot));
}

void Cast(object o, int nSpell)
{
    AssignCommand(o, ActionCastSpellAtObject(nSpell, o, METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
}

void Read(object o)
{
    string k = GetTag(o);
    PLogI(k + ".str", GetAbilityScore(o, ABILITY_STRENGTH) - GetLocalInt(o, "str"));
    PLogI(k + ".hide", GetSkillRank(SKILL_HIDE, o) - GetLocalInt(o, "hide"));
    PLogI(k + ".fort", GetFortitudeSavingThrow(o) - GetLocalInt(o, "fort"));
    int n = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { n++; e = GetNextEffect(o); }
    PLogI(k + ".effects", n);
}

void main()
{
    // Mode 4 types: Bull's Strength and Cat's Grace insight (they do not stack), Endurance morale.
    NWNX_NoStack_SetSpellBonusType(SPELL_BULLS_STRENGTH, NWNX_NOSTACK_EFFECT_TYPE_INSIGHT);
    NWNX_NoStack_SetSpellBonusType(SPELL_CATS_GRACE, NWNX_NOSTACK_EFFECT_TYPE_INSIGHT);
    NWNX_NoStack_SetSpellBonusType(SPELL_ENDURANCE, NWNX_NOSTACK_EFFECT_TYPE_MORALE);

    object oUp = Make("UP", -4.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 4), oUp);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_HIDE, 4), oUp);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowIncrease(SAVING_THROW_FORT, 4), oUp);
    Ring(oUp, INVENTORY_SLOT_LEFTRING, ItemPropertyAbilityBonus(IP_CONST_ABILITY_STR, 2), ItemPropertySkillBonus(SKILL_HIDE, 2),
        ItemPropertyBonusSavingThrow(IP_CONST_SAVEBASETYPE_FORTITUDE, 2));
    Ring(oUp, INVENTORY_SLOT_RIGHTRING, ItemPropertyAbilityBonus(IP_CONST_ABILITY_STR, 1), ItemPropertySkillBonus(SKILL_HIDE, 1),
        ItemPropertyBonusSavingThrow(IP_CONST_SAVEBASETYPE_FORTITUDE, 1));
    DelayCommand(1.0, Cast(oUp, SPELL_BULLS_STRENGTH));
    DelayCommand(1.5, Cast(oUp, SPELL_CATS_GRACE));
    DelayCommand(2.0, Cast(oUp, SPELL_ENDURANCE));

    object oDown = Make("DOWN", 4.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_STRENGTH, 1), oDown);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillDecrease(SKILL_HIDE, 1), oDown);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowDecrease(SAVING_THROW_FORT, 1), oDown);
    Ring(oDown, INVENTORY_SLOT_LEFTRING, ItemPropertyDecreaseAbility(IP_CONST_ABILITY_STR, 3), ItemPropertyDecreaseSkill(SKILL_HIDE, 3),
        ItemPropertyReducedSavingThrow(IP_CONST_SAVEBASETYPE_FORTITUDE, 3));
    DelayCommand(1.0, Cast(oDown, SPELL_OWLS_WISDOM));

    object oMix = Make("MIX", 0.0);
    Ring(oMix, INVENTORY_SLOT_LEFTRING, ItemPropertyAbilityBonus(IP_CONST_ABILITY_STR, 2), ItemPropertySkillBonus(SKILL_HIDE, 2),
        ItemPropertyBonusSavingThrow(IP_CONST_SAVEBASETYPE_FORTITUDE, 2));
    DelayCommand(1.0, Cast(oMix, SPELL_BULLS_STRENGTH));
    DelayCommand(1.5, Cast(oMix, SPELL_OWLS_WISDOM));

    DelayCommand(10.0, Read(oUp));
    DelayCommand(10.0, Read(oDown));
    DelayCommand(10.0, Read(oMix));
    DelayCommand(10.5, PDone());
}
