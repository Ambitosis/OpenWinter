// Charges, uses per day, and stores: gold, prices, identify cost, stock and items created in them.
#include "parity_items"

string Charges(string sResRef)
{
    object c = GetObjectByTag("CHGCHEST");
    string s = "";
    int i;
    int nValues = 9;
    for (i = 0; i < nValues; i++)
    {
        int v = i == 0 ? -5 : i == 1 ? 0 : i == 2 ? 1 : i == 3 ? 49 : i == 4 ? 50 : i == 5 ? 51 : i == 6 ? 255 : i == 7 ? 256 : 1000;
        object o = CreateItemOnObject(sResRef, c);
        SetItemCharges(o, v);
        s += IntToString(v) + ":" + IntToString(GetItemCharges(o)) + " ";
        SetLocalObject(c, sResRef + IntToString(i), o);
    }
    return s;
}

void ChargesValid(string sResRef)
{
    object c = GetObjectByTag("CHGCHEST");
    string s = "";
    int i;
    for (i = 0; i < 9; i++) s += IntToString(GetIsObjectValid(GetLocalObject(c, sResRef + IntToString(i)))) + " ";
    PLog("charges." + sResRef + ".valid", s);
}

void ChargesTo0(string sResRef)
{
    object c = GetObjectByTag("CHGCHEST");
    object o = CreateItemOnObject(sResRef, c);
    int n = GetItemCharges(o);
    SetItemCharges(o, n - 1);
    SetItemCharges(o, 0);
    PLog("charges." + sResRef + ".to0", IntToString(n) + " " + IntToString(GetItemCharges(o)) + " " + IntToString(GetIsObjectValid(o)));
    SetLocalObject(c, sResRef + "to0", o);
}

void Charging()
{
    CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-4.0, 4.0), FALSE, "CHGCHEST");
}

void Charging2()
{
    PLog("charges.nw_wmgwn006", Charges("nw_wmgwn006"));
    PLog("charges.nw_wswls001", Charges("nw_wswls001"));
    PLog("charges.nw_it_mpotion001", Charges("nw_it_mpotion001"));
    PLog("charges.nw_it_medkit001", Charges("nw_it_medkit001"));
    ChargesTo0("nw_wmgwn006");
    ChargesTo0("nw_wswls001");
}

void Charging3()
{
    ChargesValid("nw_wmgwn006");
    ChargesValid("nw_wswls001");
    ChargesValid("nw_it_mpotion001");
    object c = GetObjectByTag("CHGCHEST");
    PLogI("charges.wand.to0.valid", GetIsObjectValid(GetLocalObject(c, "nw_wmgwn006to0")));
    PLogI("charges.sword.to0.valid", GetIsObjectValid(GetLocalObject(c, "nw_wswls001to0")));
}

void Uses()
{
    object c = GetObjectByTag("CHGCHEST");
    object o = CreateItemOnObject("nw_wswls001", c);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_HASTE_10, IP_CONST_CASTSPELL_NUMUSES_3_USES_PER_DAY), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_LIGHT_5, IP_CONST_CASTSPELL_NUMUSES_UNLIMITED_USE), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), o);
    itemproperty ip = GetFirstItemProperty(o);
    string s = "";
    while (GetIsItemPropertyValid(ip))
    {
        s += IntToString(GetItemPropertyType(ip)) + ":" + IntToString(GetItemPropertyUsesPerDayRemaining(o, ip));
        int v;
        for (v = -1; v <= 5; v += 2)
        {
            SetItemPropertyUsesPerDayRemaining(o, ip, v);
            s += "," + IntToString(GetItemPropertyUsesPerDayRemaining(o, ip));
        }
        s += " ";
        ip = GetNextItemProperty(o);
    }
    PLog("uses", s);
    // A property built separately but equal to the item's.
    itemproperty ip2 = ItemPropertyCastSpell(IP_CONST_CASTSPELL_HASTE_10, IP_CONST_CASTSPELL_NUMUSES_3_USES_PER_DAY);
    PLogI("uses.copy", GetItemPropertyUsesPerDayRemaining(o, ip2));
    SetItemPropertyUsesPerDayRemaining(o, ip2, 2);
    PLogI("uses.copy.set", GetItemPropertyUsesPerDayRemaining(o, GetFirstItemProperty(o)));
    // Uses on blueprint items.
    object w = CreateItemOnObject("nw_it_mring020", c);
    s = "";
    ip = GetFirstItemProperty(w);
    while (GetIsItemPropertyValid(ip)) { s += IntToString(GetItemPropertyType(ip)) + ":" + IntToString(GetItemPropertyUsesPerDayRemaining(w, ip)) + " "; ip = GetNextItemProperty(w); }
    PLog("uses.ring020", s);
    // Per-day spells on blueprint items, before and after setting.
    object r3 = CreateItemOnObject("nw_it_mring003", c);
    ip = GetFirstItemProperty(r3);
    s = IntToString(GetItemPropertyUsesPerDayRemaining(r3, ip));
    SetItemPropertyUsesPerDayRemaining(r3, ip, 1);
    s += " " + IntToString(GetItemPropertyUsesPerDayRemaining(r3, ip));
    SetItemPropertyUsesPerDayRemaining(r3, ip, 0);
    s += " " + IntToString(GetItemPropertyUsesPerDayRemaining(r3, ip));
    SetItemPropertyUsesPerDayRemaining(r3, ip, 300);
    s += " " + IntToString(GetItemPropertyUsesPerDayRemaining(r3, ip));
    SetItemPropertyUsesPerDayRemaining(r3, ip, -2);
    s += " " + IntToString(GetItemPropertyUsesPerDayRemaining(r3, ip));
    PLog("uses.ring003", s);
    object m4 = CreateItemOnObject("nw_it_mmidmisc04", c);
    s = "";
    ip = GetFirstItemProperty(m4);
    while (GetIsItemPropertyValid(ip)) { s += IntToString(GetItemPropertyUsesPerDayRemaining(m4, ip)) + " "; ip = GetNextItemProperty(m4); }
    PLog("uses.misc04", s);
}

string StoreItems(object s)
{
    string r = "";
    int n = 0;
    object o = GetFirstItemInInventory(s);
    while (GetIsObjectValid(o))
    {
        n++;
        r += GetResRef(o) + ":" + IntToString(GetItemStackSize(o)) + ":" + IntToString(GetInfiniteFlag(o)) + ":" + IntToString(GetGoldPieceValue(o)) + ":" + IntToString(GetIdentified(o)) + " ";
        o = GetNextItemInInventory(s);
    }
    return IntToString(n) + " " + r;
}

void Store(string sResRef)
{
    object s = CreateObject(OBJECT_TYPE_STORE, sResRef, PCentre(4.0, 4.0), FALSE, "S_" + sResRef);
    string k = "store." + sResRef + ".";
    PLogI(k + "valid", GetIsObjectValid(s));
    PLogI(k + "gold", GetStoreGold(s));
    PLogI(k + "maxbuy", GetStoreMaxBuyPrice(s));
    PLogI(k + "identify", GetStoreIdentifyCost(s));
    PLog(k + "items", StoreItems(s));
}

void Stores()
{
    Store("nw_storeweap001");
    Store("nw_storgenral001");
    Store("nw_storemagic001");
    Store("nw_storethief001");
    Store("nw_storebar01");
    Store("x2_genie");
    object s = GetObjectByTag("S_nw_storeweap001");
    SetStoreGold(s, 500); PLogI("store.set.gold", GetStoreGold(s));
    SetStoreGold(s, -5); PLogI("store.set.gold.neg", GetStoreGold(s));
    SetStoreMaxBuyPrice(s, 100); PLogI("store.set.maxbuy", GetStoreMaxBuyPrice(s));
    SetStoreMaxBuyPrice(s, -7); PLogI("store.set.maxbuy.neg", GetStoreMaxBuyPrice(s));
    SetStoreIdentifyCost(s, 0); PLogI("store.set.identify0", GetStoreIdentifyCost(s));
    SetStoreIdentifyCost(s, -3); PLogI("store.set.identify.neg", GetStoreIdentifyCost(s));
    PLogI("store.notstore.gold", GetStoreGold(GetObjectByTag("CHGCHEST")));
    PLogI("store.notstore.maxbuy", GetStoreMaxBuyPrice(GetObjectByTag("CHGCHEST")));
    PLogI("store.notstore.identify", GetStoreIdentifyCost(GetObjectByTag("CHGCHEST")));
    PLogI("store.invalid.gold", GetStoreGold(OBJECT_INVALID));
    // Items created in a store.
    object a = CreateItemOnObject("nw_wswls001", s);
    PLog("store.created", GetTag(GetItemPossessor(a)) + " inf=" + IntToString(GetInfiniteFlag(a)) + " gpv=" + IntToString(GetGoldPieceValue(a)));
    object b = CreateItemOnObject("nw_wamar001", s, 20);
    object b2 = CreateItemOnObject("nw_wamar001", s, 20);
    PLog("store.created.arrows", IntToString(b == b2) + " " + IntToString(GetItemStackSize(b)) + " " + IntToString(GetItemStackSize(b2)));
    PLogI("store.fits", GetBaseItemFitsInInventory(BASE_ITEM_TOWERSHIELD, s));
    PLogI("store.hasinv", GetHasInventory(s));
    PLog("store.first", GetResRef(GetFirstItemInInventory(s)));
    SetInfiniteFlag(a, TRUE);
    PLogI("store.setinfinite", GetInfiniteFlag(a));
    object e = CreateItemOnObject("nw_wswls001", GetObjectByTag("CHGCHEST"));
    SetInfiniteFlag(e, TRUE);
    PLogI("store.setinfinite.notstore", GetInfiniteFlag(e));
}

void Finish() { PDone(); }

void main()
{
    DelayCommand(0.5, Charging());
    DelayCommand(1.0, Charging2());
    DelayCommand(1.5, Charging3());
    DelayCommand(1.6, Uses());
    DelayCommand(1.8, Stores());
    DelayCommand(2.5, Finish());
}
