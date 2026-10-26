// NWNX_Creature special abilities (list, add, set, remove) and the restore calls for feats, special abilities
// and items, read back through the base game's own functions.
#include "parity_inc"
#include "nwnx_creature"

void List(string k, object o)
{
    int n = NWNX_Creature_GetSpecialAbilityCount(o);
    string s = IntToString(n) + ":";
    int i;
    for (i = 0; i <= n; i++)
    {
        struct NWNX_Creature_SpecialAbility a = NWNX_Creature_GetSpecialAbility(o, i);
        s += " " + IntToString(a.id) + "/" + IntToString(a.ready) + "/" + IntToString(a.level);
    }
    PLog(k + ".list", s);
    s = IntToString(GetSpellAbilityCount(o)) + ":";
    for (i = 0; i < GetSpellAbilityCount(o); i++)
        s += " " + IntToString(GetSpellAbilitySpell(o, i)) + "/" + IntToString(GetSpellAbilityReady(o, i)) + "/" + IntToString(GetSpellAbilityCasterLevel(o, i));
    PLog(k + ".native", s);
    PLog(k + ".has", IntToString(GetHasSpell(16, o)) + " " + IntToString(GetHasSpell(SPELL_LIGHT, o)) + " " + IntToString(GetHasSpell(SPELL_BLESS, o)));
}

void main()
{
    object oD = CreateObject(OBJECT_TYPE_CREATURE, "nw_dryad", PCentre());
    List("start", oD);
    struct NWNX_Creature_SpecialAbility a;
    a.id = SPELL_LIGHT; a.ready = 1; a.level = 5;
    NWNX_Creature_AddSpecialAbility(oD, a);
    List("add", oD);
    a.id = SPELL_BLESS; a.ready = 0; a.level = 7;
    NWNX_Creature_SetSpecialAbility(oD, 0, a);
    List("set", oD);
    NWNX_Creature_RemoveSpecialAbility(oD, 1);
    List("remove", oD);
    NWNX_Creature_RemoveSpecialAbility(oD, 9);
    a.id = SPELL_LIGHT; a.ready = 1; a.level = 2;
    NWNX_Creature_SetSpecialAbility(oD, 9, a);
    List("badindex", oD);
    DecrementRemainingSpellUses(oD, 16);
    DecrementRemainingSpellUses(oD, SPELL_LIGHT);
    List("used", oD);
    NWNX_Creature_RestoreSpecialAbilities(oD);
    List("restored", oD);
    a.id = SPELL_LIGHT; a.ready = 1; a.level = 3;
    object oB = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(2.0));
    NWNX_Creature_AddSpecialAbility(oB, a);
    List("fresh", oB);

    // Feat uses.
    NWNX_Creature_AddFeat(oB, FEAT_BARBARIAN_RAGE);
    NWNX_Creature_AddFeat(oB, FEAT_STUNNING_FIST);
    PLog("feat.start", IntToString(NWNX_Creature_GetFeatRemainingUses(oB, FEAT_BARBARIAN_RAGE)) + " " + IntToString(NWNX_Creature_GetFeatRemainingUses(oB, FEAT_STUNNING_FIST))
        + " " + IntToString(NWNX_Creature_GetFeatTotalUses(oB, FEAT_BARBARIAN_RAGE)) + " " + IntToString(NWNX_Creature_GetFeatTotalUses(oB, FEAT_STUNNING_FIST))
        + " " + IntToString(NWNX_Creature_GetFeatTotalUses(oB, FEAT_POWER_ATTACK)) + " " + IntToString(NWNX_Creature_GetFeatRemainingUses(oB, FEAT_POWER_ATTACK)));
    DecrementRemainingFeatUses(oB, FEAT_BARBARIAN_RAGE);
    NWNX_Creature_SetFeatRemainingUses(oB, FEAT_STUNNING_FIST, 0);
    PLog("feat.used", IntToString(NWNX_Creature_GetFeatRemainingUses(oB, FEAT_BARBARIAN_RAGE)) + " " + IntToString(NWNX_Creature_GetFeatRemainingUses(oB, FEAT_STUNNING_FIST))
        + " " + IntToString(GetHasFeat(FEAT_BARBARIAN_RAGE, oB)) + " " + IntToString(GetHasFeat(FEAT_STUNNING_FIST, oB)) + " " + IntToString(NWNX_Creature_GetKnowsFeat(oB, FEAT_STUNNING_FIST)));
    NWNX_Creature_SetFeatRemainingUses(oB, FEAT_STUNNING_FIST, 9);
    PLogI("feat.set9", NWNX_Creature_GetFeatRemainingUses(oB, FEAT_STUNNING_FIST));
    NWNX_Creature_SetFeatRemainingUses(oB, FEAT_STUNNING_FIST, 0);
    NWNX_Creature_RestoreFeats(oB);
    PLog("feat.restored", IntToString(NWNX_Creature_GetFeatRemainingUses(oB, FEAT_BARBARIAN_RAGE)) + " " + IntToString(NWNX_Creature_GetFeatRemainingUses(oB, FEAT_STUNNING_FIST))
        + " " + IntToString(GetHasFeat(FEAT_BARBARIAN_RAGE, oB)) + " " + IntToString(GetHasFeat(FEAT_STUNNING_FIST, oB)));
    PLogI("feat.highest", NWNX_Creature_GetHighestLevelOfFeat(oB, FEAT_BARBARIAN_RAGE));

    // Item uses per day and charges.
    object oI = CreateItemOnObject("nw_it_mring021", oB);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_LIGHT_1, IP_CONST_CASTSPELL_NUMUSES_2_USES_PER_DAY), oI);
    object oW = CreateItemOnObject("nw_wmgwn006", oB);
    PLogI("item.charges", GetItemCharges(oW));
    SetItemCharges(oW, 1);
    itemproperty ip = GetFirstItemProperty(oI);
    while (GetIsItemPropertyValid(ip) && GetItemPropertyType(ip) != ITEM_PROPERTY_CAST_SPELL) ip = GetNextItemProperty(oI);
    PLogI("item.uses", GetItemPropertyUsesPerDayRemaining(oI, ip));
    SetItemPropertyUsesPerDayRemaining(oI, ip, 0);
    PLogI("item.used", GetItemPropertyUsesPerDayRemaining(oI, ip));
    NWNX_Creature_RestoreItems(oB);
    PLogI("item.restored", GetItemPropertyUsesPerDayRemaining(oI, ip));
    PLogI("item.chargesafter", GetItemCharges(oW));
    PDone();
}
