// Item properties on the wearer: AC types and stacking with effects, abilities, skills, saves, immunities,
// spell resistance and the effects equipped items put on a creature. A fighter equips one item per step.
#include "parity_items"

const string WEARER = "WEARER";

json GffInt(int n)
{
    return JsonObjectSet(JsonObjectSet(JsonObject(), "type", JsonString("int")), "value", JsonInt(n));
}

// A plain item of any base item type: a longsword's GFF with another BaseItem.
object MakeBase(int nBase, object oOwner)
{
    object oTemplate = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre(0.0, -5.0));
    json j = ObjectToJson(oTemplate);
    DestroyObject(oTemplate);
    j = JsonObjectSet(j, "BaseItem", GffInt(nBase));
    return JsonToObject(j, GetLocation(oOwner), oOwner);
}

// Equipped item properties don't show up as effects; count what GetFirstEffect returns.
string Effects(object o)
{
    int nAll = 0, nItem = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        nAll++;
        if (GetObjectType(GetEffectCreator(e)) == OBJECT_TYPE_ITEM) nItem++;
        e = GetNextEffect(o);
    }
    return IntToString(nAll) + " from items " + IntToString(nItem);
}

void Snap(string k)
{
    object o = GetObjectByTag(WEARER);
    k = "w." + k + ".";
    PLogI(k + "ac", GetAC(o));
    string a = "";
    int i;
    for (i = 0; i < 6; i++) a += IntToString(GetAbilityScore(o, i)) + "/" + IntToString(GetAbilityScore(o, i, TRUE)) + " ";
    PLog(k + "abilities", a);
    PLog(k + "skills", IntToString(GetSkillRank(SKILL_HIDE, o)) + " " + IntToString(GetSkillRank(SKILL_SPOT, o)) + " "
        + IntToString(GetSkillRank(SKILL_DISCIPLINE, o)) + " " + IntToString(GetSkillRank(SKILL_CONCENTRATION, o)) + " "
        + IntToString(GetSkillRank(SKILL_TUMBLE, o)) + " " + IntToString(GetSkillRank(SKILL_HIDE, o, TRUE)));
    PLog(k + "saves", IntToString(GetFortitudeSavingThrow(o)) + " " + IntToString(GetReflexSavingThrow(o)) + " " + IntToString(GetWillSavingThrow(o)));
    PLogI(k + "sr", GetSpellResistance(o));
    string im = "";
    for (i = 0; i <= 32; i++) if (GetIsImmune(o, i)) im += IntToString(i) + " ";
    PLog(k + "immune", im);
    PLogI(k + "speed", GetMovementRate(o));
    PLogI(k + "asf", GetArcaneSpellFailure(o));
    PLogI(k + "feat.alertness", GetHasFeat(FEAT_ALERTNESS, o));
    PLog(k + "effects", Effects(o));
    string sl = "";
    for (i = 0; i < NUM_INVENTORY_SLOTS; i++) { object it = GetItemInSlot(i, o); if (GetIsObjectValid(it)) sl += IntToString(i) + ":" + IntToString(GetBaseItemType(it)) + " "; }
    PLog(k + "slots", sl);
}

object Make(int nBase)
{
    return MakeBase(nBase, GetObjectByTag(WEARER));
}

void Equip(object oItem, int nSlot)
{
    object o = GetObjectByTag(WEARER);
    AssignCommand(o, ActionEquipItem(oItem, nSlot));
}

void Prop(object oItem, itemproperty ip) { AddItemProperty(DURATION_TYPE_PERMANENT, ip, oItem); }

void Step(int n)
{
    object o = GetObjectByTag(WEARER);
    object it;
    if (n == 0)
    {
        // Strip the spawn treasure; the guard keeps its starting equipment.
        it = GetFirstItemInInventory(o);
        while (GetIsObjectValid(it)) { DestroyObject(it); it = GetNextItemInInventory(o); }
        SetAILevel(o, AI_LEVEL_NORMAL);
    }
    else if (n == 1) { it = CreateItemOnObject("nw_aarcl001", o); Prop(it, ItemPropertyACBonus(2)); Equip(it, INVENTORY_SLOT_CHEST); }
    else if (n == 2) { it = CreateItemOnObject("nw_ashlw001", o); Prop(it, ItemPropertyACBonus(1)); Equip(it, INVENTORY_SLOT_LEFTHAND); }
    else if (n == 3) { it = Make(BASE_ITEM_RING); Prop(it, ItemPropertyACBonus(1)); Prop(it, ItemPropertyAbilityBonus(IP_CONST_ABILITY_STR, 4)); Equip(it, INVENTORY_SLOT_LEFTRING); }
    else if (n == 4) { it = Make(BASE_ITEM_RING); Prop(it, ItemPropertyACBonus(3)); Prop(it, ItemPropertyAbilityBonus(IP_CONST_ABILITY_STR, 2)); Prop(it, ItemPropertySkillBonus(SKILL_HIDE, 3)); Equip(it, INVENTORY_SLOT_RIGHTRING); }
    else if (n == 5) { it = Make(BASE_ITEM_AMULET); Prop(it, ItemPropertyACBonus(2)); Prop(it, ItemPropertyBonusSavingThrow(IP_CONST_SAVEBASETYPE_REFLEX, 3)); Prop(it, ItemPropertyBonusSpellResistance(IP_CONST_SPELLRESISTANCEBONUS_14)); Equip(it, INVENTORY_SLOT_NECK); }
    else if (n == 6) { it = Make(BASE_ITEM_BOOTS); Prop(it, ItemPropertyACBonus(2)); Prop(it, ItemPropertyHaste()); Prop(it, ItemPropertySkillBonus(SKILL_TUMBLE, 4)); Equip(it, INVENTORY_SLOT_BOOTS); }
    else if (n == 7) { it = Make(BASE_ITEM_CLOAK); Prop(it, ItemPropertyACBonus(1)); Prop(it, ItemPropertyBonusSavingThrowVsX(IP_CONST_SAVEVS_UNIVERSAL, 2)); Prop(it, ItemPropertyReducedSavingThrow(IP_CONST_SAVEBASETYPE_WILL, 1)); Equip(it, INVENTORY_SLOT_CLOAK); }
    else if (n == 8) { it = Make(BASE_ITEM_GLOVES); Prop(it, ItemPropertySkillBonus(SKILL_HIDE, 5)); Prop(it, ItemPropertyAbilityBonus(IP_CONST_ABILITY_DEX, 3)); Prop(it, ItemPropertyDecreaseAbility(IP_CONST_ABILITY_CHA, 2)); Equip(it, INVENTORY_SLOT_ARMS); }
    else if (n == 9) { it = Make(BASE_ITEM_BELT); Prop(it, ItemPropertyDamageResistance(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGERESIST_10)); Prop(it, ItemPropertyDamageImmunity(IP_CONST_DAMAGETYPE_COLD, IP_CONST_DAMAGEIMMUNITY_50_PERCENT)); Prop(it, ItemPropertyDamageVulnerability(IP_CONST_DAMAGETYPE_ACID, IP_CONST_DAMAGEVULNERABILITY_25_PERCENT)); Prop(it, ItemPropertyDamageReduction(IP_CONST_DAMAGEREDUCTION_2, IP_CONST_DAMAGESOAK_5_HP)); Equip(it, INVENTORY_SLOT_BELT); }
    else if (n == 10) { it = Make(BASE_ITEM_HELMET); Prop(it, ItemPropertyFreeAction()); Prop(it, ItemPropertyTrueSeeing()); Prop(it, ItemPropertyDarkvision()); Prop(it, ItemPropertyImmunityMisc(IP_CONST_IMMUNITYMISC_DEATH_MAGIC)); Prop(it, ItemPropertyBonusFeat(IP_CONST_FEAT_ALERTNESS)); Prop(it, ItemPropertyRegeneration(2)); Equip(it, INVENTORY_SLOT_HEAD); }
    else if (n == 11) { it = GetItemInSlot(INVENTORY_SLOT_RIGHTRING, o); Prop(it, ItemPropertyAbilityBonus(IP_CONST_ABILITY_CON, 2)); }
    else if (n == 12) { it = GetItemInSlot(INVENTORY_SLOT_BOOTS, o); AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyAbilityBonus(IP_CONST_ABILITY_WIS, 2), it, 2.0); }
    else if (n == 13) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2, AC_DEFLECTION_BONUS), o); ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(1, AC_DODGE_BONUS), o); ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 3), o); ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_HIDE, 4), o); }
    else if (n == 14) { it = GetItemInSlot(INVENTORY_SLOT_LEFTRING, o); itemproperty ip = GetFirstItemProperty(it); RemoveItemProperty(it, ip); }
    else if (n == 15) { AssignCommand(o, ActionUnequipItem(GetItemInSlot(INVENTORY_SLOT_RIGHTRING, o))); }
    else if (n == 16) { it = Make(BASE_ITEM_RING); Prop(it, ItemPropertyACBonus(5)); Prop(it, ItemPropertyLimitUseByClass(IP_CONST_CLASS_WIZARD)); Equip(it, INVENTORY_SLOT_RIGHTRING); }
    else if (n == 17) { it = Make(BASE_ITEM_RING); Prop(it, ItemPropertyAbilityBonus(IP_CONST_ABILITY_STR, 12)); Prop(it, ItemPropertySkillBonus(SKILL_HIDE, 50)); Prop(it, ItemPropertyACBonus(20)); Equip(it, INVENTORY_SLOT_RIGHTRING); }
    else if (n == 18) { it = CreateItemOnObject("nw_wswls001", o); Prop(it, ItemPropertyEnhancementBonus(2)); Prop(it, ItemPropertyACBonus(3)); Prop(it, ItemPropertyAbilityBonus(IP_CONST_ABILITY_INT, 2)); Equip(it, INVENTORY_SLOT_RIGHTHAND); }
    else if (n == 19) { it = CreateItemOnObject("nw_ashto001", o); Prop(it, ItemPropertyArcaneSpellFailure(IP_CONST_ARCANE_SPELL_FAILURE_MINUS_10_PERCENT)); Equip(it, INVENTORY_SLOT_LEFTHAND); }
    else if (n == 20) { AssignCommand(o, ActionUnequipItem(GetItemInSlot(INVENTORY_SLOT_CHEST, o))); AssignCommand(o, ActionUnequipItem(GetItemInSlot(INVENTORY_SLOT_NECK, o))); }
}

void Run(int n)
{
    Step(n);
    DelayCommand(0.8, Snap(IntToString(n)));
    if (n < 20) DelayCommand(1.0, Run(n + 1));
    else DelayCommand(1.5, PDone());
}

void main()
{
    CreateObject(OBJECT_TYPE_CREATURE, "nw_guard", PCentre(), FALSE, WEARER);
    DelayCommand(1.0, Snap("start"));
    DelayCommand(1.5, Run(0));
}
