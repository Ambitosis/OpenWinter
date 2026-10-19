// Effects package test: item properties.
int CountProps(object oItem)
{
    int n = 0;
    itemproperty ip = GetFirstItemProperty(oItem);
    while (GetIsItemPropertyValid(ip))
    {
        n++;
        ip = GetNextItemProperty(oItem);
    }
    return n;
}

void main()
{
    object oM = GetModule();
    object oItem = GetObjectByTag("fx_item");

    itemproperty ipFire = ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6);
    SetLocalInt(oM, "fire_type", GetItemPropertyType(ipFire));
    SetLocalInt(oM, "fire_sub", GetItemPropertySubType(ipFire));
    SetLocalInt(oM, "fire_table", GetItemPropertyCostTable(ipFire));
    SetLocalInt(oM, "fire_value", GetItemPropertyCostTableValue(ipFire));
    SetLocalInt(oM, "fire_param1", GetItemPropertyParam1(ipFire));

    itemproperty ipRace = ItemPropertyDamageBonusVsRace(IP_CONST_RACIALTYPE_UNDEAD, IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_2);
    SetLocalInt(oM, "race_sub", GetItemPropertySubType(ipRace));
    SetLocalInt(oM, "race_param1", GetItemPropertyParam1(ipRace));
    SetLocalInt(oM, "race_param1value", GetItemPropertyParam1Value(ipRace));
    SetLocalInt(oM, "race_value", GetItemPropertyCostTableValue(ipRace));

    itemproperty ipLight = ItemPropertyLight(IP_CONST_LIGHTBRIGHTNESS_BRIGHT, IP_CONST_LIGHTCOLOR_BLUE);
    SetLocalInt(oM, "light_param1", GetItemPropertyParam1(ipLight));
    SetLocalInt(oM, "light_param1value", GetItemPropertyParam1Value(ipLight));

    itemproperty ipCustom = ItemPropertyCustom(ITEM_PROPERTY_KEEN);
    SetLocalInt(oM, "custom_type", GetItemPropertyType(ipCustom));

    AddItemProperty(DURATION_TYPE_PERMANENT, TagItemProperty(ipFire, "fire"), oItem);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyHaste(), oItem, 5.0);
    AddItemProperty(DURATION_TYPE_PERMANENT, ipRace, oItem);
    AddItemProperty(DURATION_TYPE_INSTANT, ItemPropertyKeen(), oItem);
    SetLocalInt(oM, "ip_count", CountProps(oItem));
    SetLocalInt(oM, "has_haste", GetItemHasItemProperty(oItem, ITEM_PROPERTY_HASTE));
    SetLocalInt(oM, "has_keen", GetItemHasItemProperty(oItem, ITEM_PROPERTY_KEEN));

    itemproperty ip = GetFirstItemProperty(oItem);
    while (GetIsItemPropertyValid(ip))
    {
        if (GetItemPropertyType(ip) == ITEM_PROPERTY_HASTE)
        {
            SetLocalInt(oM, "haste_durtype", GetItemPropertyDurationType(ip));
            SetLocalInt(oM, "haste_remaining", GetItemPropertyDurationRemaining(ip));
        }
        if (GetItemPropertyTag(ip) == "fire") RemoveItemProperty(oItem, ip);
        ip = GetNextItemProperty(oItem);
    }
    SetLocalInt(oM, "after_remove", CountProps(oItem));
    // A copy the script kept is matched by content.
    RemoveItemProperty(oItem, ipRace);
    SetLocalInt(oM, "after_copy_remove", CountProps(oItem));
}
