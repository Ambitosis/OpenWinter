// Item property constructors and getters, adding, removing and iterating properties.
#include "parity_items"

void K(string k, itemproperty ip)
{
    k = "ctor." + k;
    PLogI(k + ".valid", GetIsItemPropertyValid(ip));
    PLog(k + ".fields", PIpKey(ip));
    PLog(k + ".tag", GetItemPropertyTag(ip));
    PLogI(k + ".dur", GetItemPropertyDuration(ip));
}

void Constructors()
{
    K("ability", ItemPropertyAbilityBonus(IP_CONST_ABILITY_STR, 3));
    K("ability.0", ItemPropertyAbilityBonus(IP_CONST_ABILITY_CHA, 0));
    K("ability.13", ItemPropertyAbilityBonus(IP_CONST_ABILITY_DEX, 13));
    K("ability.bad", ItemPropertyAbilityBonus(9, 2));
    K("ac", ItemPropertyACBonus(2));
    K("ac.0", ItemPropertyACBonus(0));
    K("ac.21", ItemPropertyACBonus(21));
    K("ac.neg", ItemPropertyACBonus(-2));
    K("acalign", ItemPropertyACBonusVsAlign(IP_CONST_ALIGNMENTGROUP_EVIL, 3));
    K("acdmg", ItemPropertyACBonusVsDmgType(IP_CONST_DAMAGETYPE_SLASHING, 4));
    K("acrace", ItemPropertyACBonusVsRace(IP_CONST_RACIALTYPE_UNDEAD, 5));
    K("acsalign", ItemPropertyACBonusVsSAlign(IP_CONST_ALIGNMENT_CE, 1));
    K("enh", ItemPropertyEnhancementBonus(3));
    K("enh.20", ItemPropertyEnhancementBonus(20));
    K("enhalign", ItemPropertyEnhancementBonusVsAlign(IP_CONST_ALIGNMENTGROUP_GOOD, 2));
    K("enhrace", ItemPropertyEnhancementBonusVsRace(IP_CONST_RACIALTYPE_DRAGON, 4));
    K("enhsalign", ItemPropertyEnhancementBonusVsSAlign(IP_CONST_ALIGNMENT_LG, 1));
    K("enhpen", ItemPropertyEnhancementPenalty(2));
    K("enhpen.neg", ItemPropertyEnhancementPenalty(-2));
    K("weightred", ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_60_PERCENT));
    K("feat", ItemPropertyBonusFeat(IP_CONST_FEAT_ALERTNESS));
    K("feat.bad", ItemPropertyBonusFeat(5000));
    K("spellslot", ItemPropertyBonusLevelSpell(IP_CONST_CLASS_WIZARD, IP_CONST_SPELLLEVEL_3));
    K("castspell", ItemPropertyCastSpell(IP_CONST_CASTSPELL_FIREBALL_5, IP_CONST_CASTSPELL_NUMUSES_1_USE_PER_DAY));
    K("castspell.bad", ItemPropertyCastSpell(22, 1));
    K("dmg", ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6));
    K("dmgalign", ItemPropertyDamageBonusVsAlign(IP_CONST_ALIGNMENTGROUP_EVIL, IP_CONST_DAMAGETYPE_DIVINE, IP_CONST_DAMAGEBONUS_2d6));
    K("dmgrace", ItemPropertyDamageBonusVsRace(IP_CONST_RACIALTYPE_UNDEAD, IP_CONST_DAMAGETYPE_POSITIVE, IP_CONST_DAMAGEBONUS_1d8));
    K("dmgsalign", ItemPropertyDamageBonusVsSAlign(IP_CONST_ALIGNMENT_CE, IP_CONST_DAMAGETYPE_SONIC, IP_CONST_DAMAGEBONUS_3));
    K("dmgimm", ItemPropertyDamageImmunity(IP_CONST_DAMAGETYPE_COLD, IP_CONST_DAMAGEIMMUNITY_50_PERCENT));
    K("dmgpen", ItemPropertyDamagePenalty(3));
    K("dr", ItemPropertyDamageReduction(IP_CONST_DAMAGEREDUCTION_3, IP_CONST_DAMAGESOAK_10_HP));
    K("dres", ItemPropertyDamageResistance(IP_CONST_DAMAGETYPE_ACID, IP_CONST_DAMAGERESIST_15));
    K("dvul", ItemPropertyDamageVulnerability(IP_CONST_DAMAGETYPE_ELECTRICAL, IP_CONST_DAMAGEVULNERABILITY_25_PERCENT));
    K("darkvision", ItemPropertyDarkvision());
    K("decability", ItemPropertyDecreaseAbility(IP_CONST_ABILITY_WIS, 2));
    K("decability.neg", ItemPropertyDecreaseAbility(IP_CONST_ABILITY_WIS, -2));
    K("decac", ItemPropertyDecreaseAC(IP_CONST_ACMODIFIERTYPE_ARMOR, 3));
    K("decskill", ItemPropertyDecreaseSkill(SKILL_HIDE, 4));
    K("contweight", ItemPropertyContainerReducedWeight(IP_CONST_CONTAINERWEIGHTRED_40_PERCENT));
    K("extramelee", ItemPropertyExtraMeleeDamageType(IP_CONST_DAMAGETYPE_PIERCING));
    K("extraranged", ItemPropertyExtraRangeDamageType(IP_CONST_DAMAGETYPE_BLUDGEONING));
    K("haste", ItemPropertyHaste());
    K("holy", ItemPropertyHolyAvenger());
    K("immmisc", ItemPropertyImmunityMisc(IP_CONST_IMMUNITYMISC_DEATH_MAGIC));
    K("impevasion", ItemPropertyImprovedEvasion());
    K("sr", ItemPropertyBonusSpellResistance(IP_CONST_SPELLRESISTANCEBONUS_14));
    K("savevsx", ItemPropertyBonusSavingThrowVsX(IP_CONST_SAVEVS_FEAR, 2));
    K("save", ItemPropertyBonusSavingThrow(IP_CONST_SAVEBASETYPE_REFLEX, 3));
    K("keen", ItemPropertyKeen());
    K("light", ItemPropertyLight(IP_CONST_LIGHTBRIGHTNESS_BRIGHT, IP_CONST_LIGHTCOLOR_BLUE));
    K("mighty", ItemPropertyMaxRangeStrengthMod(4));
    K("nodamage", ItemPropertyNoDamage());
    K("onhit", ItemPropertyOnHitProps(IP_CONST_ONHIT_HOLD, IP_CONST_ONHIT_SAVEDC_16, IP_CONST_ONHIT_DURATION_25_PERCENT_3_ROUNDS));
    K("onhit.abil", ItemPropertyOnHitProps(IP_CONST_ONHIT_ABILITYDRAIN, IP_CONST_ONHIT_SAVEDC_20, IP_CONST_ABILITY_CON));
    K("onhit.nospecial", ItemPropertyOnHitProps(IP_CONST_ONHIT_DAZE, IP_CONST_ONHIT_SAVEDC_14));
    K("redsavevsx", ItemPropertyReducedSavingThrowVsX(IP_CONST_SAVEVS_ACID, 2));
    K("redsave", ItemPropertyReducedSavingThrow(IP_CONST_SAVEBASETYPE_WILL, 1));
    K("regen", ItemPropertyRegeneration(2));
    K("skill", ItemPropertySkillBonus(SKILL_SPOT, 5));
    K("skill.50", ItemPropertySkillBonus(SKILL_SPOT, 50));
    K("spellimm", ItemPropertySpellImmunitySpecific(IP_CONST_IMMUNITYSPELL_DARKNESS));
    K("schoolimm", ItemPropertySpellImmunitySchool(IP_CONST_SPELLSCHOOL_NECROMANCY));
    K("thieves", ItemPropertyThievesTools(6));
    K("attack", ItemPropertyAttackBonus(2));
    K("attack.21", ItemPropertyAttackBonus(21));
    K("attackalign", ItemPropertyAttackBonusVsAlign(IP_CONST_ALIGNMENTGROUP_CHAOTIC, 3));
    K("attackrace", ItemPropertyAttackBonusVsRace(IP_CONST_RACIALTYPE_ELF, 2));
    K("attacksalign", ItemPropertyAttackBonusVsSAlign(IP_CONST_ALIGNMENT_NE, 1));
    K("attackpen", ItemPropertyAttackPenalty(2));
    K("unlammo", ItemPropertyUnlimitedAmmo());
    K("unlammo.fire", ItemPropertyUnlimitedAmmo(IP_CONST_UNLIMITEDAMMO_1D6FIRE));
    K("limitalign", ItemPropertyLimitUseByAlign(IP_CONST_ALIGNMENTGROUP_LAWFUL));
    K("limitclass", ItemPropertyLimitUseByClass(IP_CONST_CLASS_PALADIN));
    K("limitrace", ItemPropertyLimitUseByRace(IP_CONST_RACIALTYPE_DWARF));
    K("limitsalign", ItemPropertyLimitUseBySAlign(IP_CONST_ALIGNMENT_TN));
    K("vampregen", ItemPropertyVampiricRegeneration(3));
    K("trap", ItemPropertyTrap(IP_CONST_TRAPSTRENGTH_STRONG, IP_CONST_TRAPTYPE_FIRE));
    K("trueseeing", ItemPropertyTrueSeeing());
    K("onmonsterhit", ItemPropertyOnMonsterHitProperties(IP_CONST_ONMONSTERHIT_POISON, 3));
    K("turnres", ItemPropertyTurnResistance(5));
    K("massive", ItemPropertyMassiveCritical(IP_CONST_DAMAGEBONUS_2d6));
    K("freeaction", ItemPropertyFreeAction());
    K("monsterdmg", ItemPropertyMonsterDamage(IP_CONST_MONSTERDAMAGE_1d6));
    K("spelllevel", ItemPropertyImmunityToSpellLevel(3));
    K("walk", ItemPropertySpecialWalk());
    K("walk.1", ItemPropertySpecialWalk(1));
    K("healers", ItemPropertyHealersKit(4));
    K("weightinc", ItemPropertyWeightIncrease(IP_CONST_WEIGHTINCREASE_10_LBS));
    K("onhitspell", ItemPropertyOnHitCastSpell(IP_CONST_ONHIT_CASTSPELL_FIREBALL, 7));
    K("vfx", ItemPropertyVisualEffect(ITEM_VISUAL_FIRE));
    K("asf", ItemPropertyArcaneSpellFailure(IP_CONST_ARCANE_SPELL_FAILURE_MINUS_10_PERCENT));
    K("material", ItemPropertyMaterial(3));
    K("quality", ItemPropertyQuality(IP_CONST_QUALITY_GOOD));
    K("additional", ItemPropertyAdditional(IP_CONST_ADDITIONAL_CURSED));
    K("custom.haste", ItemPropertyCustom(ITEM_PROPERTY_HASTE));
    K("custom.ability", ItemPropertyCustom(ITEM_PROPERTY_ABILITY_BONUS, 2, 4));
    K("custom.bad", ItemPropertyCustom(200));
    K("custom.neg", ItemPropertyCustom(-1));
    K("custom.sub.big", ItemPropertyCustom(ITEM_PROPERTY_ABILITY_BONUS, 6, 1));
    K("custom.cost.big", ItemPropertyCustom(ITEM_PROPERTY_ABILITY_BONUS, 1, 13));
    K("custom.cost.over", ItemPropertyCustom(ITEM_PROPERTY_ABILITY_BONUS, 1, 14));
    K("custom.nosub.sub", ItemPropertyCustom(ITEM_PROPERTY_HASTE, 3, 2, 1));
    K("custom.light", ItemPropertyCustom(ITEM_PROPERTY_LIGHT, -1, 2, 3));
    K("custom.dmgalign", ItemPropertyCustom(ITEM_PROPERTY_DAMAGE_BONUS_VS_ALIGNMENT_GROUP, 1, 4, 2));
    K("custom.dmgalign.noparam", ItemPropertyCustom(ITEM_PROPERTY_DAMAGE_BONUS_VS_ALIGNMENT_GROUP, 1, 4));
    K("custom.onhit", ItemPropertyCustom(ITEM_PROPERTY_ON_HIT_PROPERTIES, 2, 3, 1));
    K("tagged", TagItemProperty(ItemPropertyHaste(), "mytag"));
    K("invalid", GetFirstItemProperty(OBJECT_INVALID));
}

string Types(object o)
{
    string s = "";
    itemproperty ip = GetFirstItemProperty(o);
    while (GetIsItemPropertyValid(ip)) { s += IntToString(GetItemPropertyType(ip)) + " "; ip = GetNextItemProperty(o); }
    return s;
}

void Adding()
{
    object o = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre());
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(1), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(1), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(2), o);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyHaste(), o, 60.0);
    AddItemProperty(DURATION_TYPE_INSTANT, ItemPropertyDarkvision(), o);
    AddItemProperty(3, ItemPropertyTrueSeeing(), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyMaxRangeStrengthMod(2), o); // not on melee weapons
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCustom(200), o);
    PLog("add.list", PIpList(o));
    PLog("add.types", Types(o));
    PLogI("add.has.enh", GetItemHasItemProperty(o, ITEM_PROPERTY_ENHANCEMENT_BONUS));
    PLogI("add.has.haste", GetItemHasItemProperty(o, ITEM_PROPERTY_HASTE));
    PLogI("add.has.mighty", GetItemHasItemProperty(o, ITEM_PROPERTY_MIGHTY));
    PLogI("add.gpv", GetGoldPieceValue(o));
    // Remove the second +1 while iterating.
    itemproperty ip = GetFirstItemProperty(o);
    int n = 0;
    while (GetIsItemPropertyValid(ip))
    {
        if (GetItemPropertyType(ip) == ITEM_PROPERTY_ENHANCEMENT_BONUS && GetItemPropertyCostTableValue(ip) == 1)
        {
            n++;
            if (n == 2) RemoveItemProperty(o, ip);
        }
        ip = GetNextItemProperty(o);
    }
    PLog("remove.same", PIpList(o));
    DelayCommand(0.1, PLog("remove.after", PIpList(o)));
    // Remove an equal property built separately.
    RemoveItemProperty(o, ItemPropertyKeen());
    PLog("remove.copy", PIpList(o));
    DelayCommand(0.1, PLog("remove.copy.after", PIpList(o)));
    // Remove with the very variable that was added.
    itemproperty ipKept = ItemPropertyAttackBonus(2);
    AddItemProperty(DURATION_TYPE_PERMANENT, ipKept, o);
    PLog("remove.kept.before", PIpList(o));
    RemoveItemProperty(o, ipKept);
    PLog("remove.kept", PIpList(o));
    itemproperty ipTemp = ItemPropertyAttackBonus(4);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ipTemp, o, 20.0);
    RemoveItemProperty(o, ipTemp);
    PLog("remove.kept.temp", PIpList(o));
    // Iterating while removing everything.
    object o2 = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre());
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(1), o2);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), o2);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyAttackBonus(1), o2);
    ip = GetFirstItemProperty(o2);
    string s = "";
    while (GetIsItemPropertyValid(ip))
    {
        s += IntToString(GetItemPropertyType(ip)) + " ";
        RemoveItemProperty(o2, ip);
        ip = GetNextItemProperty(o2);
    }
    PLog("removeall.seen", s);
    PLog("removeall.now", PIpList(o2));
    PLogI("removeall.gpv", GetGoldPieceValue(o2));
    DelayCommand(0.1, PLog("removeall.after", PIpList(o2)));
    DelayCommand(0.1, PLogI("removeall.gpv.after", GetGoldPieceValue(o2)));
    // Nested iteration over two items.
    object o3 = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre());
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(1), o3);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), o3);
    s = "";
    ip = GetFirstItemProperty(o3);
    while (GetIsItemPropertyValid(ip))
    {
        s += IntToString(GetItemPropertyType(ip)) + "[" + Types(o) + "] ";
        ip = GetNextItemProperty(o3);
    }
    PLog("nested", s);
    // Blueprint properties then added ones.
    object o4 = CreateObject(OBJECT_TYPE_ITEM, "nw_wswmls002", PCentre());
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), o4);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyAttackBonus(3), o4, 30.0);
    PLog("bp.list", PIpList(o4));
    ip = GetFirstItemProperty(o4);
    while (GetIsItemPropertyValid(ip))
    {
        string k = "bp.ip" + IntToString(GetItemPropertyType(ip));
        PLogI(k + ".durtype", GetItemPropertyDurationType(ip));
        PLogI(k + ".dur", GetItemPropertyDuration(ip));
        PLogI(k + ".remain", GetItemPropertyDurationRemaining(ip));
        ip = GetNextItemProperty(o4);
    }
}

// Temporary properties expire: check what's left as time passes.
void Expiry(object o, float t)
{
    string s = "";
    itemproperty ip = GetFirstItemProperty(o);
    while (GetIsItemPropertyValid(ip))
    {
        s += IntToString(GetItemPropertyType(ip)) + ":" + IntToString(GetItemPropertyDurationRemaining(ip)) + " ";
        ip = GetNextItemProperty(o);
    }
    PLog("expiry." + FloatToString(t, 0, 1), s);
    PLogI("expiry.gpv." + FloatToString(t, 0, 1), GetGoldPieceValue(o));
}

void Temporary()
{
    object o = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre());
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyEnhancementBonus(2), o, 3.2);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyKeen(), o, 6.3);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyHaste(), o, 0.0);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyAttackBonus(1), o, -5.0);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyDarkvision(), o);
    float t;
    for (t = 0.0; t <= 9.0; t += 1.0) DelayCommand(t + 0.5, Expiry(o, t + 0.5));
}

void Finish() { PDone(); }

void main()
{
    DelayCommand(1.0, Constructors());
    DelayCommand(1.5, Adding());
    DelayCommand(2.0, Temporary());
    DelayCommand(13.0, Finish());
}
