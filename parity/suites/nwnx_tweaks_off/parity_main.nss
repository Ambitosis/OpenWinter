// NWNX_Tweaks that change script-visible rules: StringToInt's base, the value of charged items, merging stacks with
// different variables, equip events for every creature and items kept at 0 charges. nwnx_tweaks_off runs the same
// script with no tweaks set.
#include "parity_inc"

void Num(string s)
{
    PLogI("s2i." + s, StringToInt(s));
}

void Used(object oUser)
{
    object oWand = GetLocalObject(oUser, "wand");
    PLogI("depleted.valid", GetIsObjectValid(oWand));
    PLogI("depleted.charges", GetItemCharges(oWand));
}

void Use(object oUser)
{
    object oWand = GetLocalObject(oUser, "wand");
    object oTarget = GetLocalObject(oUser, "target");
    itemproperty ip = GetFirstItemProperty(oWand);
    while (GetIsItemPropertyValid(ip) && GetItemPropertyType(ip) != ITEM_PROPERTY_CAST_SPELL) ip = GetNextItemProperty(oWand);
    PLogI("depleted.before", GetItemCharges(oWand));
    AssignCommand(oUser, ActionUseItemOnObject(oWand, ip, oTarget));
}

void Dex(object o)
{
    PLogI("dex.held", GetAbilityScore(o, ABILITY_DEXTERITY));
}

void Done()
{
    PDone();
}

void main()
{
    Num("10");
    Num("0x1A");
    Num("0X1f");
    Num("010");
    Num("08");
    Num("-0x10");
    Num("  0x10");
    Num("+017");
    Num("0x");
    Num("0b11");
    Num("1e3");
    Num("abc");
    Num("0xFFFFFFFF");

    object oChest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(0.0, 4.0));
    int n;
    int nCharges = 1;
    for (n = 0; n < 9; n++)
    {
        nCharges = n == 0 ? 1 : n == 1 ? 10 : n == 2 ? 25 : n == 3 ? 49 : n == 4 ? 50 : n == 5 ? 51 : n == 6 ? 100 : n == 7 ? 200 : 250;
        object oWand = CreateItemOnObject("nw_wmgwn004", oChest);
        itemproperty ipw = GetFirstItemProperty(oWand);
        while (GetIsItemPropertyValid(ipw)) { RemoveItemProperty(oWand, ipw); ipw = GetFirstItemProperty(oWand); }
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(66, IP_CONST_CASTSPELL_NUMUSES_1_CHARGE_PER_USE), oWand);
        SetItemCharges(oWand, nCharges);
        SetIdentified(oWand, TRUE);
        PLogI("cost." + IntToString(nCharges), GetGoldPieceValue(oWand));
    }

    object oBox = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(2.0, 4.0));
    object p1 = CreateItemOnObject("nw_it_mpotion001", oBox);
    SetLocalInt(p1, "v", 1);
    object p2 = CreateItemOnObject("nw_it_mpotion001", oBox);
    PLogI("merge.fresh", p1 == p2);
    PLogI("merge.fresh.stack", GetItemStackSize(p1));
    object p3 = CopyItem(p1, oBox, TRUE);
    PLogI("merge.copy", p3 == p1);
    PLogI("merge.copy.stack", GetItemStackSize(p1));
    object p4 = CopyItem(p1, oBox, FALSE);
    PLogI("merge.copynovars", p4 == p1 || p4 == p2);
    int nItems = 0;
    object o = GetFirstItemInInventory(oBox);
    while (GetIsObjectValid(o)) { nItems++; o = GetNextItemInInventory(oBox); }
    PLogI("merge.items", nItems);

    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-4.0, 0.0));
    SetTag(oGob, "GOB");
    int e;
    for (e = 5000; e <= 5012; e++) SetEventScript(oGob, e, "");
    object oSword = CreateItemOnObject("nw_wswss001", oGob);
    SetTag(oSword, "SWORD");
    AssignCommand(oGob, ActionEquipItem(oSword, INVENTORY_SLOT_RIGHTHAND));

    object oUser = CreateObject(OBJECT_TYPE_CREATURE, "nw_elfmage015", PCentre(4.0, -2.0));
    SetPlotFlag(oUser, TRUE);
    SetAILevel(oUser, AI_LEVEL_NORMAL);
    for (e = 5000; e <= 5012; e++) SetEventScript(oUser, e, "");
    ChangeToStandardFaction(oUser, STANDARD_FACTION_COMMONER);
    object oTarget = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(4.0, 1.0));
    SetPlotFlag(oTarget, TRUE);
    for (e = 5000; e <= 5012; e++) SetEventScript(oTarget, e, "");
    ChangeToStandardFaction(oTarget, STANDARD_FACTION_COMMONER);
    object oUseWand = CreateItemOnObject("nw_wmgwn004", oUser);
    itemproperty ip = GetFirstItemProperty(oUseWand);
    while (GetIsItemPropertyValid(ip)) { RemoveItemProperty(oUseWand, ip); ip = GetFirstItemProperty(oUseWand); }
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(167, IP_CONST_CASTSPELL_NUMUSES_1_CHARGE_PER_USE), oUseWand);
    SetItemCharges(oUseWand, 1);
    SetIdentified(oUseWand, TRUE);
    SetLocalObject(oUser, "wand", oUseWand);
    SetLocalObject(oUser, "target", oUser);
    object oHeld = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-4.0, 4.0));
    for (e = 5000; e <= 5012; e++) SetEventScript(oHeld, e, "");
    PLogI("dex.free", GetAbilityScore(oHeld, ABILITY_DEXTERITY));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectParalyze(), oHeld);
    DelayCommand(1.0, Dex(oHeld));
    DelayCommand(4.0, Use(oUser));
    DelayCommand(18.0, Used(oUser));
    DelayCommand(19.0, Done());
}
