// Module item events, logged in order: each event script appends one word to a log on the module, and
// suites write the log out (PEvFlush) at points they choose. Used by items_events, items_give, items_saved.
#include "parity_items"

// An object by tag (resref when it has none); "-" when invalid, "MOD" for the module.
string PEvName(object o)
{
    if (!GetIsObjectValid(o)) return "-";
    if (o == GetModule()) return "MOD";
    string t = GetTag(o);
    return t == "" ? GetResRef(o) : t;
}

void PEvAdd(string s)
{
    object m = GetModule();
    SetLocalString(m, "PEV", GetLocalString(m, "PEV") + " " + s);
}

// The log so far ("-" when empty); clears it.
string PEvFlush()
{
    object m = GetModule();
    string s = GetLocalString(m, "PEV");
    DeleteLocalString(m, "PEV");
    return s == "" ? "-" : GetSubString(s, 1, 100000);
}

void PEvLog(string sKey) { PLog(sKey, PEvFlush()); }

// The INVENTORY_SLOT_* an item is in on a creature, or -1.
int PEvSlot(object oItem, object oCreature)
{
    int n;
    for (n = 0; n < NUM_INVENTORY_SLOTS; n++) if (GetItemInSlot(n, oCreature) == oItem) return n;
    return -1;
}

// Equipped items then inventory items of a creature, by tag.
string PEvItems(object o)
{
    string s = "";
    int n;
    for (n = 0; n < NUM_INVENTORY_SLOTS; n++)
    {
        object it = GetItemInSlot(n, o);
        if (GetIsObjectValid(it)) s += IntToString(n) + "=" + PEvName(it) + " ";
    }
    object it = GetFirstItemInInventory(o);
    while (GetIsObjectValid(it)) { s += PEvName(it) + " "; it = GetNextItemInInventory(o); }
    return s;
}
