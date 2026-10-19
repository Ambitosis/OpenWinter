// Value, weight and AC by base item type (items made from JSON), and value with several properties,
// penalties, temporary properties, stacks and identification.
#include "parity_items"

json GffInt(int n)
{
    return JsonObjectSet(JsonObjectSet(JsonObject(), "type", JsonString("int")), "value", JsonInt(n));
}

// An item of any base item type: a longsword's GFF with another BaseItem.
object MakeBase(int nBase, location l)
{
    object oTemplate = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", l);
    json j = ObjectToJson(oTemplate);
    DestroyObject(oTemplate);
    j = JsonObjectSet(j, "BaseItem", GffInt(nBase));
    return JsonToObject(j, l);
}

void BaseItem(int nBase)
{
    object o = MakeBase(nBase, PCentre());
    string k = "base." + IntToString(nBase) + ".";
    PItemCore(k, o);
    if (!GetIsObjectValid(o)) return;
    SetItemStackSize(o, 10000);
    PLogI(k + "stackmax", GetItemStackSize(o));
    PLogI(k + "gpvmax", GetGoldPieceValue(o));
    PLogI(k + "weightmax", GetWeight(o));
    SetItemStackSize(o, 1);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyLimitUseByClass(IP_CONST_CLASS_FIGHTER), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(1), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyACBonus(1), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_CURE_LIGHT_WOUNDS_2, IP_CONST_CASTSPELL_NUMUSES_SINGLE_USE), o);
    PLog(k + "props", PIpList(o));
    PLogI(k + "gpv.props", GetGoldPieceValue(o));
    PLogI(k + "ac.props", GetItemACValue(o));
    SetIdentified(o, FALSE);
    PLogI(k + "gpv.unid", GetGoldPieceValue(o));
    PLogI(k + "ident.unid", GetIdentified(o));
    DestroyObject(o);
}

void BaseItems(int nFrom, int nTo)
{
    int i;
    for (i = nFrom; i <= nTo; i++) BaseItem(i);
}

// A longsword with a list of properties added one at a time; logs the value after each.
void Combo(string k, itemproperty ip1, itemproperty ip2, itemproperty ip3, itemproperty ip4)
{
    object o = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre());
    string s = IntToString(GetGoldPieceValue(o));
    AddItemProperty(DURATION_TYPE_PERMANENT, ip1, o); s += " " + IntToString(GetGoldPieceValue(o));
    AddItemProperty(DURATION_TYPE_PERMANENT, ip2, o); s += " " + IntToString(GetGoldPieceValue(o));
    AddItemProperty(DURATION_TYPE_PERMANENT, ip3, o); s += " " + IntToString(GetGoldPieceValue(o));
    AddItemProperty(DURATION_TYPE_PERMANENT, ip4, o); s += " " + IntToString(GetGoldPieceValue(o));
    PLog("combo." + k, s);
    DestroyObject(o);
}

void Combos()
{
    itemproperty none = ItemPropertyCustom(-1);
    Combo("enh", ItemPropertyEnhancementBonus(1), ItemPropertyEnhancementBonus(2), ItemPropertyEnhancementBonus(5), ItemPropertyEnhancementBonus(10));
    Combo("mix", ItemPropertyEnhancementBonus(3), ItemPropertyKeen(), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6), ItemPropertyHaste());
    Combo("penalty", ItemPropertyEnhancementBonus(2), ItemPropertyAttackPenalty(2), ItemPropertyDecreaseAbility(IP_CONST_ABILITY_STR, 3), ItemPropertyDamagePenalty(5));
    Combo("penaltyonly", ItemPropertyAttackPenalty(5), ItemPropertyDecreaseAC(IP_CONST_ACMODIFIERTYPE_ARMOR, 5), ItemPropertyEnhancementPenalty(5), ItemPropertyDecreaseSkill(SKILL_HIDE, 10));
    Combo("vuln", ItemPropertyDamageVulnerability(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEVULNERABILITY_50_PERCENT), ItemPropertyReducedSavingThrow(IP_CONST_SAVEBASETYPE_WILL, 3), ItemPropertyReducedSavingThrowVsX(IP_CONST_SAVEVS_FEAR, 2), ItemPropertyNoDamage());
    Combo("limits", ItemPropertyLimitUseByAlign(IP_CONST_ALIGNMENTGROUP_GOOD), ItemPropertyLimitUseByRace(IP_CONST_RACIALTYPE_ELF), ItemPropertyLimitUseBySAlign(IP_CONST_ALIGNMENT_LG), ItemPropertyVisualEffect(ITEM_VISUAL_HOLY));
    Combo("big", ItemPropertyEnhancementBonus(20), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_DIVINE, IP_CONST_DAMAGEBONUS_2d12), ItemPropertyMassiveCritical(IP_CONST_DAMAGEBONUS_2d12), ItemPropertyAttackBonus(20));
    Combo("spells", ItemPropertyCastSpell(IP_CONST_CASTSPELL_FIREBALL_10, IP_CONST_CASTSPELL_NUMUSES_UNLIMITED_USE), ItemPropertyCastSpell(IP_CONST_CASTSPELL_HASTE_10, IP_CONST_CASTSPELL_NUMUSES_3_USES_PER_DAY), ItemPropertyCastSpell(IP_CONST_CASTSPELL_HASTE_10, IP_CONST_CASTSPELL_NUMUSES_3_USES_PER_DAY), ItemPropertyCastSpell(IP_CONST_CASTSPELL_LIGHT_5, IP_CONST_CASTSPELL_NUMUSES_SINGLE_USE));
    Combo("invalid", none, ItemPropertyKeen(), none, ItemPropertyMaxRangeStrengthMod(3));
}

void Temporary()
{
    object o = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre());
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(1), o);
    PLogI("temp.perm", GetGoldPieceValue(o));
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyEnhancementBonus(5), o, 60.0);
    PLogI("temp.added", GetGoldPieceValue(o));
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyCastSpell(IP_CONST_CASTSPELL_FIREBALL_10, IP_CONST_CASTSPELL_NUMUSES_UNLIMITED_USE), o, 60.0);
    PLogI("temp.spell", GetGoldPieceValue(o));
}

void Stacks()
{
    // Unidentified stacks, properties on stacks, plot stacks.
    object o = CreateObject(OBJECT_TYPE_ITEM, "nw_wammar003", PCentre());
    PLog("stk.arrow.props", PIpList(o));
    PLogI("stk.arrow.stack", GetItemStackSize(o));
    PLogI("stk.arrow.gpv", GetGoldPieceValue(o));
    SetIdentified(o, FALSE); PLogI("stk.arrow.unid", GetGoldPieceValue(o));
    SetItemStackSize(o, 99); PLogI("stk.arrow.unid99", GetGoldPieceValue(o));
    SetIdentified(o, TRUE); PLogI("stk.arrow.id99", GetGoldPieceValue(o));
    SetPlotFlag(o, TRUE); PLogI("stk.arrow.plot99", GetGoldPieceValue(o));
    object p = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion003", PCentre());
    PLogI("stk.pot.gpv", GetGoldPieceValue(p));
    SetItemStackSize(p, 7); PLogI("stk.pot.gpv7", GetGoldPieceValue(p));
    SetIdentified(p, FALSE); PLogI("stk.pot.unid7", GetGoldPieceValue(p));
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_HASTE_10, IP_CONST_CASTSPELL_NUMUSES_SINGLE_USE), p);
    PLogI("stk.pot.unid7.added", GetGoldPieceValue(p));
    PLogI("stk.pot.ident.added", GetIdentified(p));
    object g = CreateObject(OBJECT_TYPE_ITEM, "nw_it_gem005", PCentre());
    SetItemStackSize(g, 5);
    SetIdentified(g, FALSE); PLogI("stk.gem.unid5", GetGoldPieceValue(g));
    PLogI("stk.gem.ident", GetIdentified(g));
    object z = CreateObject(OBJECT_TYPE_ITEM, "nw_wamar001", PCentre());
    SetItemStackSize(z, 0); PLogI("stk.set0", GetItemStackSize(z));
    SetItemStackSize(z, 50); SetItemStackSize(z, -5); PLogI("stk.setneg", GetItemStackSize(z));
    SetItemStackSize(z, 99999); PLogI("stk.sethuge", GetItemStackSize(z));
    object y = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre());
    SetItemStackSize(y, 5); PLogI("stk.sword5", GetItemStackSize(y));
    // No-property items can be made unidentified.
    object m = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre());
    SetIdentified(m, FALSE); PLogI("unid.noprops.ident", GetIdentified(m));
    PLogI("unid.noprops.gpv", GetGoldPieceValue(m));
    // Charges added with SetItemCharges after a cast-spell property, then repriced by identifying.
    object w = CreateObject(OBJECT_TYPE_ITEM, "nw_wmgwn006", PCentre());
    PLogI("wand.charges", GetItemCharges(w));
    PLogI("wand.gpv", GetGoldPieceValue(w));
    SetItemCharges(w, 10); PLogI("wand.gpv10", GetGoldPieceValue(w));
    SetIdentified(w, TRUE); PLogI("wand.gpv10.id", GetGoldPieceValue(w));
    SetIdentified(w, FALSE); SetIdentified(w, TRUE); PLogI("wand.gpv10.reid", GetGoldPieceValue(w));
    object w2 = CopyItem(w, OBJECT_INVALID, TRUE); PLogI("wand.copy", GetGoldPieceValue(w2));
    SetItemCharges(w2, 40); SetItemStackSize(w2, 1); PLogI("wand.restack", GetGoldPieceValue(w2));
}

// GetItemACValue with several AC properties, conditional ones, penalties and temporary bonuses.
void ACValue(string k, string sResRef, itemproperty ip1, itemproperty ip2, itemproperty ip3)
{
    object o = CreateObject(OBJECT_TYPE_ITEM, sResRef, PCentre());
    string s = IntToString(GetItemACValue(o));
    AddItemProperty(DURATION_TYPE_PERMANENT, ip1, o); s += " " + IntToString(GetItemACValue(o));
    AddItemProperty(DURATION_TYPE_PERMANENT, ip2, o); s += " " + IntToString(GetItemACValue(o));
    AddItemProperty(DURATION_TYPE_PERMANENT, ip3, o); s += " " + IntToString(GetItemACValue(o));
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyACBonus(9), o, 60.0); s += " " + IntToString(GetItemACValue(o));
    PLog("acvalue." + k, s);
    DestroyObject(o);
}

void ACValues()
{
    ACValue("armor", "nw_aarcl004", ItemPropertyACBonus(2), ItemPropertyACBonus(4), ItemPropertyACBonus(1));
    ACValue("armor.vs", "nw_aarcl004", ItemPropertyACBonusVsRace(IP_CONST_RACIALTYPE_UNDEAD, 5), ItemPropertyACBonusVsAlign(IP_CONST_ALIGNMENTGROUP_EVIL, 3), ItemPropertyACBonusVsDmgType(IP_CONST_DAMAGETYPE_SLASHING, 4));
    ACValue("armor.dec", "nw_aarcl004", ItemPropertyDecreaseAC(IP_CONST_ACMODIFIERTYPE_ARMOR, 2), ItemPropertyACBonus(3), ItemPropertyDecreaseAC(IP_CONST_ACMODIFIERTYPE_DODGE, 1));
    ACValue("shield", "nw_ashto001", ItemPropertyACBonus(2), ItemPropertyDecreaseAC(IP_CONST_ACMODIFIERTYPE_SHIELD, 1), ItemPropertyACBonus(5));
    ACValue("ring", "nw_it_mring001", ItemPropertyACBonus(2), ItemPropertyACBonus(1), ItemPropertyACBonusVsSAlign(IP_CONST_ALIGNMENT_CE, 6));
    ACValue("cloth", "nw_cloth001", ItemPropertyACBonus(1), ItemPropertyACBonus(20), ItemPropertyDecreaseAC(IP_CONST_ACMODIFIERTYPE_ARMOR, 5));
}

void Finish() { PDone(); }

void main()
{
    DelayCommand(1.0, BaseItems(0, 40));
    DelayCommand(1.2, BaseItems(41, 80));
    DelayCommand(1.4, BaseItems(81, 120));
    DelayCommand(1.6, Combos());
    DelayCommand(1.7, Temporary());
    DelayCommand(1.8, Stacks());
    DelayCommand(2.0, ACValues());
    DelayCommand(3.0, Finish());
}
