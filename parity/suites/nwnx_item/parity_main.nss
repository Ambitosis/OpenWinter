// NWNX_Item: weight and value overrides, base item, appearance strings, base armour class, the minimum level to
// equip, and moving items between holders. NWNX_ItemProperty: unpacking, packing and active properties.
#include "parity_inc"
#include "nwnx_item"
#include "nwnx_itemprop"
#include "nwnx_object"

string O(object o) { return o == OBJECT_INVALID ? "inv" : GetIsObjectValid(o) ? GetTag(o) : "gone"; }

void PRaw(string sKey, string s)
{
    int i = 0;
    while (s != "")
    {
        WriteTimestampedLogEntry("RAW|" + sKey + "." + IntToString(i) + "|" + GetStringLeft(s, 800));
        s = GetSubString(s, 800, 1000000);
        i++;
    }
}

string Ip(struct NWNX_IPUnpacked u, int bFull = TRUE)
{
    string s = IntToString(u.nProperty) + "/" + IntToString(u.nSubType) + "/" + IntToString(u.nCostTable) + "/" + IntToString(u.nCostTableValue)
        + "/" + IntToString(u.nParam1) + "/" + IntToString(u.nParam1Value) + "/" + IntToString(u.nUsesPerDay) + "/" + IntToString(u.nChanceToAppear)
        + "/" + IntToString(u.bUsable) + "/" + u.sTag;
    if (bFull) s += "/" + IntToString(u.nSpellId) + "/" + O(u.oCreator) + "/" + IntToString(StringToInt(u.sID) > 0);
    return s;
}

void Equipped(object oWearer, object oRing)
{
    PLog("ip.equipped", O(GetItemInSlot(INVENTORY_SLOT_LEFTRING, oWearer)));
    int i;
    for (i = 0; i < 3; i++) PLog("ip.wornactive" + IntToString(i), Ip(NWNX_ItemProperty_GetActiveProperty(oRing, i), FALSE));
    PDone();
}

void main()
{
    object oChest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-3.0), FALSE, "IT_CHEST");
    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(), FALSE, "IT_GOB");
    object oPot = CreateItemOnObject("nw_it_mpotion001", oChest, 3, "IT_POT");
    object oArmor = CreateItemOnObject("nw_aarcl004", oChest, 1, "IT_ARMOR");
    object oArmor2 = CreateItemOnObject("nw_aarcl001", oChest, 1, "IT_ARMOR2");
    object oHelm = CreateItemOnObject("nw_arhe001", oChest, 1, "IT_HELM");
    object oShield = CreateItemOnObject("nw_ashlw001", oChest, 1, "IT_SHIELD");
    object oWeapon = CreateItemOnObject("nw_wblms001", oChest, 1, "IT_WEAPON");
    object oCloak = CreateItemOnObject("x2_it_mcloak001", oChest, 1, "IT_CLOAK");

    // Weight.
    PLogI("weight.pot", GetWeight(oPot));
    NWNX_Item_SetWeight(oPot, 50);
    PLogI("weight.potset", GetWeight(oPot));
    SetItemStackSize(oPot, 2);
    PLogI("weight.potstack", GetWeight(oPot));
    NWNX_Item_SetWeight(oArmor, 7);
    PLogI("weight.armor", GetWeight(oArmor));

    // Value.
    object oPot2 = CreateItemOnObject("nw_it_mpotion002", oChest, 1, "IT_POT2");
    PLog("value.start", IntToString(NWNX_Item_GetBaseGoldPieceValue(oPot2)) + "/" + IntToString(NWNX_Item_GetAddGoldPieceValue(oPot2)) + "/" + IntToString(GetGoldPieceValue(oPot2)));
    NWNX_Item_SetBaseGoldPieceValue(oPot2, 500);
    PLog("value.base", IntToString(NWNX_Item_GetBaseGoldPieceValue(oPot2)) + "/" + IntToString(GetGoldPieceValue(oPot2)));
    NWNX_Item_SetAddGoldPieceValue(oPot2, 25);
    PLog("value.add", IntToString(NWNX_Item_GetAddGoldPieceValue(oPot2)) + "/" + IntToString(GetGoldPieceValue(oPot2)));

    // Base item.
    object oPot3 = CreateItemOnObject("nw_it_mpotion003", oChest, 1, "IT_POT3");
    NWNX_Item_SetBaseItemType(oPot3, BASE_ITEM_SCROLL);
    PLogI("baseitem", GetBaseItemType(oPot3));

    // Appearance strings.
    PLog("app.pot", NWNX_Item_GetEntireItemAppearance(oPot));
    PLog("app.armor", NWNX_Item_GetEntireItemAppearance(oArmor));
    PLog("app.armor2", NWNX_Item_GetEntireItemAppearance(oArmor2));
    PLog("app.helm", NWNX_Item_GetEntireItemAppearance(oHelm));
    PLog("app.shield", NWNX_Item_GetEntireItemAppearance(oShield));
    PLog("app.weapon", NWNX_Item_GetEntireItemAppearance(oWeapon));
    PLog("app.cloak", NWNX_Item_GetEntireItemAppearance(oCloak));
    PLog("app.invalid", NWNX_Item_GetEntireItemAppearance(OBJECT_INVALID));
    NWNX_Item_RestoreItemAppearance(oArmor2, NWNX_Item_GetEntireItemAppearance(oArmor));
    PLog("app.restored", NWNX_Item_GetEntireItemAppearance(oArmor2));
    PLogI("app.restoredtorso", GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_TORSO));
    NWNX_Item_SetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_RTHIGH, 3);
    NWNX_Item_SetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1, 22);
    NWNX_Item_SetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_NUM_COLORS + (ITEM_APPR_ARMOR_MODEL_TORSO * ITEM_APPR_ARMOR_NUM_COLORS) + ITEM_APPR_ARMOR_COLOR_LEATHER1, 40);
    PLog("app.set", IntToString(GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_RTHIGH)) + "/"
        + IntToString(GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1)) + "/"
        + IntToString(GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_NUM_COLORS + (ITEM_APPR_ARMOR_MODEL_TORSO * ITEM_APPR_ARMOR_NUM_COLORS) + ITEM_APPR_ARMOR_COLOR_LEATHER1)));
    PLog("app.setstring", NWNX_Item_GetEntireItemAppearance(oArmor2));
    // Colours past the palettes' 176 rows (persistent worlds ship taller palettes).
    NWNX_Item_SetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH2, 249);
    NWNX_Item_SetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_METAL2, 255);
    NWNX_Item_SetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_NUM_COLORS + (ITEM_APPR_ARMOR_MODEL_TORSO * ITEM_APPR_ARMOR_NUM_COLORS) + ITEM_APPR_ARMOR_COLOR_METAL1, 254);
    PLog("app.highcolours", IntToString(GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH2)) + "/"
        + IntToString(GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_METAL2)) + "/"
        + IntToString(GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_NUM_COLORS + (ITEM_APPR_ARMOR_MODEL_TORSO * ITEM_APPR_ARMOR_NUM_COLORS) + ITEM_APPR_ARMOR_COLOR_METAL1)));
    PLog("app.highstring", NWNX_Item_GetEntireItemAppearance(oArmor2));
    object oHigh = CopyItemAndModify(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1, 30, TRUE);
    PLog("app.highcopy", IntToString(GetItemAppearance(oHigh, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH2)) + "/"
        + IntToString(GetItemAppearance(oHigh, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1)));
    DestroyObject(oHigh);
    // A per-part colour of 255 clears the override; values past a byte.
    int nPart = ITEM_APPR_ARMOR_NUM_COLORS + (ITEM_APPR_ARMOR_MODEL_TORSO * ITEM_APPR_ARMOR_NUM_COLORS) + ITEM_APPR_ARMOR_COLOR_METAL1;
    NWNX_Item_SetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, nPart, 255);
    NWNX_Item_SetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_LEATHER2, 300);
    NWNX_Item_SetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_METAL1, 256);
    PLog("app.partclear", IntToString(GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, nPart)) + "/"
        + IntToString(GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_LEATHER2)) + "/"
        + IntToString(GetItemAppearance(oArmor2, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_METAL1)));
    PLog("app.partclearstring", NWNX_Item_GetEntireItemAppearance(oArmor2));
    NWNX_Item_SetItemAppearance(oWeapon, ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_COLOR_TOP, 3);
    NWNX_Item_SetItemAppearance(oWeapon, ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_MIDDLE, 2);
    PLog("app.weaponset", NWNX_Item_GetEntireItemAppearance(oWeapon));
    NWNX_Item_SetItemAppearance(oPot, ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 5);
    PLog("app.simpleset", NWNX_Item_GetEntireItemAppearance(oPot));
    object oPot4 = CreateItemOnObject("nw_it_mpotion001", oChest, 1, "IT_POT4");
    NWNX_Item_RestoreItemAppearance(oPot4, "garbage");
    PLog("app.garbage", NWNX_Item_GetEntireItemAppearance(oPot4));

    // Armour class.
    PLog("ac", IntToString(NWNX_Item_GetBaseArmorClass(oArmor)) + "/" + IntToString(NWNX_Item_GetBaseArmorClass(oArmor2)) + "/"
        + IntToString(NWNX_Item_GetBaseArmorClass(oShield)) + "/" + IntToString(NWNX_Item_GetBaseArmorClass(oHelm)) + "/"
        + IntToString(NWNX_Item_GetBaseArmorClass(oWeapon)) + "/" + IntToString(NWNX_Item_GetBaseArmorClass(OBJECT_INVALID)));

    // Minimum level to equip.
    object oRich = CreateItemOnObject("nw_aarcl012", oChest, 1, "IT_RICH");
    PLog("minlevel", IntToString(NWNX_Item_GetMinEquipLevel(oPot)) + "/" + IntToString(NWNX_Item_GetMinEquipLevel(oArmor)) + "/"
        + IntToString(NWNX_Item_GetMinEquipLevel(oRich)) + "/" + IntToString(GetGoldPieceValue(oRich)));
    PLog("minlevel.mods", IntToString(NWNX_Item_GetMinEquipLevelModifier(oRich)) + "/" + IntToString(NWNX_Item_GetMinEquipLevelOverride(oRich)));
    NWNX_Item_SetMinEquipLevelModifier(oRich, 3, TRUE);
    PLog("minlevel.modifier", IntToString(NWNX_Item_GetMinEquipLevel(oRich)) + "/" + IntToString(NWNX_Item_GetMinEquipLevelModifier(oRich)));
    NWNX_Item_SetMinEquipLevelOverride(oRich, 10, TRUE);
    PLog("minlevel.override", IntToString(NWNX_Item_GetMinEquipLevel(oRich)) + "/" + IntToString(NWNX_Item_GetMinEquipLevelOverride(oRich)));
    NWNX_Item_SetMinEquipLevelModifier(oRich, -15, TRUE);
    PLog("minlevel.negative", IntToString(NWNX_Item_GetMinEquipLevel(oRich)));
    PRaw("minlevel.serial", NWNX_Object_Serialize(oRich));
    object oRichCopy = NWNX_Object_Deserialize(NWNX_Object_Serialize(oRich));
    PLog("minlevel.copy", IntToString(NWNX_Item_GetMinEquipLevelModifier(oRichCopy)) + "/" + IntToString(NWNX_Item_GetMinEquipLevelOverride(oRichCopy)));
    NWNX_Item_SetMinEquipLevelOverride(oRich, 0, TRUE);
    NWNX_Item_SetMinEquipLevelModifier(oRich, 0, TRUE);
    PLog("minlevel.cleared", IntToString(NWNX_Item_GetMinEquipLevel(oRich)) + "/" + IntToString(NWNX_Item_GetMinEquipLevelModifier(oRich)) + "/" + IntToString(NWNX_Item_GetMinEquipLevelOverride(oRich)));

    // Moving items.
    object oBag = CreateItemOnObject("nw_it_contain001", oGob, 1, "IT_BAG");
    object oMove = CreateItemOnObject("nw_it_mpotion002", oChest, 1, "IT_MOVE");
    PLogI("move.creature", NWNX_Item_MoveTo(oMove, oGob));
    PLog("move.creatureholder", O(GetItemPossessor(oMove)));
    PLogI("move.bag", NWNX_Item_MoveTo(oMove, oBag));
    PLog("move.bagholder", O(GetItemPossessor(oMove)) + "/" + O(GetItemPossessor(oMove, TRUE)));
    PLogI("move.chest", NWNX_Item_MoveTo(oMove, oChest));
    PLog("move.chestholder", O(GetItemPossessor(oMove)));
    object oStore = CreateObject(OBJECT_TYPE_STORE, "nw_storemagic001", PCentre(5.0), FALSE, "IT_STORE");
    PLogI("move.store", NWNX_Item_MoveTo(oMove, oStore));
    PLog("move.storeholder", O(GetItemPossessor(oMove)));
    object oWp = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(), FALSE, "IT_WP");
    PLogI("move.waypoint", NWNX_Item_MoveTo(oMove, oWp));
    PLogI("move.self", NWNX_Item_MoveTo(oBag, oBag));
    object oGround = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion003", PCentre(1.0), FALSE, "IT_GROUND");
    PLogI("move.ground", NWNX_Item_MoveTo(oGround, oGob));
    PLog("move.groundholder", O(GetItemPossessor(oGround)) + "/" + IntToString(GetIsObjectValid(GetArea(oGround))));
    PLogI("move.bagintobag", NWNX_Item_MoveTo(CreateItemOnObject("nw_it_contain001", oChest), oBag));

    // Item properties.
    struct NWNX_IPUnpacked u = NWNX_ItemProperty_UnpackIP(ItemPropertyAbilityBonus(IP_CONST_ABILITY_STR, 2));
    PLog("ip.str", Ip(u));
    u = NWNX_ItemProperty_UnpackIP(TagItemProperty(ItemPropertyCastSpell(IP_CONST_CASTSPELL_FIREBALL_10, IP_CONST_CASTSPELL_NUMUSES_3_USES_PER_DAY), "cast"));
    PLog("ip.cast", Ip(u));
    u = NWNX_ItemProperty_UnpackIP(ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6));
    PLog("ip.damage", Ip(u));
    itemproperty ipBad;
    u = NWNX_ItemProperty_UnpackIP(ipBad);
    PLog("ip.invalid", Ip(u));
    u = NWNX_ItemProperty_UnpackIP(ItemPropertyAbilityBonus(IP_CONST_ABILITY_DEX, 1));
    u.nCostTableValue = 4;
    u.sTag = "packed";
    itemproperty ipP = NWNX_ItemProperty_PackIP(u);
    PLog("ip.packed", IntToString(GetItemPropertyType(ipP)) + "/" + IntToString(GetItemPropertySubType(ipP)) + "/" + IntToString(GetItemPropertyCostTableValue(ipP)) + "/" + GetItemPropertyTag(ipP) + "/" + IntToString(GetIsItemPropertyValid(ipP)));
    object oRing = CreateItemOnObject("nw_it_mring001", oChest, 1, "IT_RING");
    AddItemProperty(DURATION_TYPE_PERMANENT, ipP, oRing);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyAbilityBonus(IP_CONST_ABILITY_CON, 1), oRing, 60.0);
    int i;
    string sProps = "";
    itemproperty ipI = GetFirstItemProperty(oRing);
    while (GetIsItemPropertyValid(ipI)) { sProps += " " + IntToString(GetItemPropertyType(ipI)) + ":" + GetItemPropertyTag(ipI); ipI = GetNextItemProperty(oRing); }
    PLog("ip.ringprops", sProps);
    for (i = 0; i < 4; i++) PLog("ip.active" + IntToString(i), Ip(NWNX_ItemProperty_GetActiveProperty(oRing, i), FALSE));
    object oCast = CreateItemOnObject("nw_it_mpotion001", oChest, 1, "IT_CASTPOT");
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyAbilityBonus(IP_CONST_ABILITY_WIS, 1), oCast);
    AddItemProperty(DURATION_TYPE_PERMANENT, TagItemProperty(ItemPropertyCastSpell(IP_CONST_CASTSPELL_FIREBALL_10, IP_CONST_CASTSPELL_NUMUSES_3_USES_PER_DAY), "fb"), oCast);
    for (i = 0; i < 4; i++) PLog("ip.castactive" + IntToString(i), Ip(NWNX_ItemProperty_GetActiveProperty(oCast, i), FALSE));
    PLog("ip.activebad", Ip(NWNX_ItemProperty_GetActiveProperty(OBJECT_INVALID, 0), FALSE));
    PLog("ip.applied", Ip(NWNX_ItemProperty_UnpackIP(GetFirstItemProperty(oRing))));
    // Active properties are those of equipped items.
    object oWearer = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(2.0), FALSE, "IT_WEARER");
    SetAILevel(oWearer, AI_LEVEL_NORMAL);
    object oRing2 = CreateItemOnObject("nw_it_mring001", oWearer, 1, "IT_RING2");
    AddItemProperty(DURATION_TYPE_PERMANENT, TagItemProperty(ItemPropertySkillBonus(SKILL_HIDE, 2), "hide"), oRing2);
    AssignCommand(oWearer, ActionEquipItem(oRing2, INVENTORY_SLOT_LEFTRING));
    DelayCommand(1.0, Equipped(oWearer, oRing2));
}
