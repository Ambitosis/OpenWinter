// Item measurements shared by the item suites.
#include "parity_inc"

// "type.subtype.costtable.costvalue.param1.param1value.duration" for one property.
string PIpKey(itemproperty ip)
{
    return IntToString(GetItemPropertyType(ip)) + "." + IntToString(GetItemPropertySubType(ip)) + "."
        + IntToString(GetItemPropertyCostTable(ip)) + "." + IntToString(GetItemPropertyCostTableValue(ip)) + "."
        + IntToString(GetItemPropertyParam1(ip)) + "." + IntToString(GetItemPropertyParam1Value(ip)) + "."
        + IntToString(GetItemPropertyDurationType(ip));
}

// Every property on an item, in iteration order, separated by spaces.
string PIpList(object o)
{
    string s = "";
    itemproperty ip = GetFirstItemProperty(o);
    while (GetIsItemPropertyValid(ip))
    {
        if (s != "") s += " ";
        s += PIpKey(ip);
        ip = GetNextItemProperty(o);
    }
    return s;
}

// Value, weight, AC, stack and charges of one item.
void PItemCore(string k, object o)
{
    if (!GetIsObjectValid(o)) { PLog(k + "valid", "0"); return; }
    PLogI(k + "base", GetBaseItemType(o));
    PLogI(k + "gpv", GetGoldPieceValue(o));
    PLogI(k + "weight", GetWeight(o));
    PLogI(k + "ac", GetItemACValue(o));
    PLogI(k + "stack", GetItemStackSize(o));
    PLogI(k + "charges", GetItemCharges(o));
    PLogI(k + "ident", GetIdentified(o));
}

// How many items an object's inventory iteration returns.
int PCountItems(object oHolder)
{
    int n = 0;
    object o = GetFirstItemInInventory(oHolder);
    while (GetIsObjectValid(o)) { n++; o = GetNextItemInInventory(oHolder); }
    return n;
}
