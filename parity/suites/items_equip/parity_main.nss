// Equip restrictions: slots, proficiency, two-handed weapons and shields, size, possession, ammunition.
// Each step queues ActionEquipItem/ActionUnequipItem on a creature and logs its slots a moment later.
#include "parity_items"

string Slots(object o)
{
    string s = "";
    int i;
    for (i = 0; i < NUM_INVENTORY_SLOTS; i++)
    {
        object it = GetItemInSlot(i, o);
        if (GetIsObjectValid(it)) s += IntToString(i) + ":" + GetTag(it) + " ";
    }
    return s;
}

object Give(object o, string sResRef, string sTag, int nStack = 1)
{
    return CreateItemOnObject(sResRef, o, nStack, sTag);
}

void Eq(object o, string sTag, int nSlot) { AssignCommand(o, ActionEquipItem(GetObjectByTag(sTag), nSlot)); }
void Uneq(object o, string sTag) { AssignCommand(o, ActionUnequipItem(GetObjectByTag(sTag))); }

void Step(int n)
{
    object a = GetObjectByTag("EQA"); // half-orc bard-ish: martial, simple, shields, light and medium armour
    object b = GetObjectByTag("EQB"); // small (gnome): simple weapons only
    object c = GetObjectByTag("EQC"); // elf cleric: all armour, shields, simple
    if (n == 0)
    {
        object it = GetFirstItemInInventory(a);
        while (GetIsObjectValid(it)) { DestroyObject(it); it = GetNextItemInInventory(a); }
        it = GetFirstItemInInventory(b);
        while (GetIsObjectValid(it)) { DestroyObject(it); it = GetNextItemInInventory(b); }
        it = GetFirstItemInInventory(c);
        while (GetIsObjectValid(it)) { DestroyObject(it); it = GetNextItemInInventory(c); }
        SetAILevel(a, AI_LEVEL_NORMAL); SetAILevel(b, AI_LEVEL_NORMAL); SetAILevel(c, AI_LEVEL_NORMAL);
    }
    else if (n == 1)
    {
        Give(a, "nw_ashsw001", "A_SHIELD"); Give(a, "nw_wswgs001", "A_GREAT"); Give(a, "nw_wswls001", "A_LONG"); Give(a, "nw_wswdg001", "A_DAGGER");
        Give(a, "nw_wplss001", "A_SPEAR"); Give(a, "nw_wbwln001", "A_BOW"); Give(a, "nw_wamar001", "A_ARROWS", 20); Give(a, "nw_wambo001", "A_BOLTS", 20);
        Give(a, "nw_aarcl004", "A_MEDIUM"); Give(a, "nw_aarcl007", "A_HEAVY"); Give(a, "nw_ashto001", "A_TOWER"); Give(a, "nw_it_mring001", "A_RING");
        Give(a, "nw_it_creitem005", "A_HIDE"); Give(a, "nw_wthdt001", "A_DARTS", 10);
        Eq(a, "A_SHIELD", INVENTORY_SLOT_LEFTHAND);
    }
    else if (n == 2) Eq(a, "A_GREAT", INVENTORY_SLOT_RIGHTHAND);          // two-handed with a shield
    else if (n == 3) Eq(a, "A_SHIELD", INVENTORY_SLOT_LEFTHAND);          // shield with a two-hander
    else if (n == 4) { Eq(a, "A_LONG", INVENTORY_SLOT_RIGHTHAND); Eq(a, "A_DAGGER", INVENTORY_SLOT_LEFTHAND); }
    else if (n == 5) Eq(a, "A_DAGGER", INVENTORY_SLOT_RIGHTHAND);         // from the off hand to the main hand
    else if (n == 6) Eq(a, "A_LONG", INVENTORY_SLOT_LEFTHAND);            // longsword in the off hand
    else if (n == 7) Eq(a, "A_SPEAR", INVENTORY_SLOT_LEFTHAND);           // two-hander in the off hand
    else if (n == 8) { Eq(a, "A_BOW", INVENTORY_SLOT_RIGHTHAND); Eq(a, "A_ARROWS", INVENTORY_SLOT_ARROWS); Eq(a, "A_BOLTS", INVENTORY_SLOT_ARROWS); }
    else if (n == 9) { Eq(a, "A_BOLTS", INVENTORY_SLOT_BOLTS); Eq(a, "A_DARTS", INVENTORY_SLOT_RIGHTHAND); }
    else if (n == 10) { Eq(a, "A_RING", INVENTORY_SLOT_HEAD); Eq(a, "A_RING", INVENTORY_SLOT_RIGHTRING); }
    else if (n == 11) Eq(a, "A_RING", INVENTORY_SLOT_LEFTRING);           // ring moves to the other hand
    else if (n == 12) { Eq(a, "A_HEAVY", INVENTORY_SLOT_CHEST); }        // no heavy armour proficiency
    else if (n == 13) { Eq(a, "A_MEDIUM", INVENTORY_SLOT_CHEST); }
    else if (n == 14) { Eq(a, "A_TOWER", INVENTORY_SLOT_LEFTHAND); Eq(a, "A_HIDE", INVENTORY_SLOT_CARMOUR); }
    else if (n == 15)
    {
        // Not possessed: on the ground, in someone else's inventory, in a bag.
        object g = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mring001", GetLocation(a), FALSE, "A_GROUNDRING");
        Give(c, "nw_it_mring001", "C_RING");
        object bag = Give(a, "nw_it_contain001", "A_BAG");
        CreateItemOnObject("nw_it_mneck020", bag, 1, "A_BAGNECK");
        Eq(a, "A_GROUNDRING", INVENTORY_SLOT_RIGHTRING);
        Eq(a, "C_RING", INVENTORY_SLOT_LEFTRING);
        Eq(a, "A_BAGNECK", INVENTORY_SLOT_NECK);
    }
    else if (n == 16)
    {
        PLog("bagneck.possessor", GetTag(GetItemPossessor(GetObjectByTag("A_BAGNECK"))));
        PLog("groundring.possessor", GetTag(GetItemPossessor(GetObjectByTag("A_GROUNDRING"))));
        object u = Give(a, "nw_it_mring002", "A_UNID");
        SetIdentified(u, FALSE);
        Eq(a, "A_UNID", INVENTORY_SLOT_RIGHTRING);
        Uneq(a, "A_MEDIUM");
    }
    else if (n == 17)
    {
        Give(b, "nw_wswls001", "B_LONG"); Give(b, "nw_wswdg001", "B_DAGGER"); Give(b, "nw_wdbqs001", "B_STAFF");
        Give(b, "nw_wplss001", "B_SPEAR"); Give(b, "nw_ashsw001", "B_SHIELD"); Give(b, "nw_wbwxl001", "B_XBOW");
        Eq(b, "B_LONG", INVENTORY_SLOT_RIGHTHAND);                       // no martial proficiency
    }
    else if (n == 18) Eq(b, "B_SPEAR", INVENTORY_SLOT_RIGHTHAND);         // simple, large for a small creature
    else if (n == 19) Eq(b, "B_SHIELD", INVENTORY_SLOT_LEFTHAND);
    else if (n == 20) Eq(b, "B_DAGGER", INVENTORY_SLOT_RIGHTHAND);
    else if (n == 21) Eq(b, "B_SHIELD", INVENTORY_SLOT_LEFTHAND);
    else if (n == 22) Eq(b, "B_STAFF", INVENTORY_SLOT_RIGHTHAND);
    else if (n == 23) Eq(b, "B_XBOW", INVENTORY_SLOT_RIGHTHAND);
    else if (n == 24)
    {
        Give(c, "nw_aarcl007", "C_HEAVY"); Give(c, "nw_ashto001", "C_TOWER"); Give(c, "nw_wswls001", "C_LONG"); Give(c, "nw_wbwsh001", "C_SHORTBOW");
        Eq(c, "C_HEAVY", INVENTORY_SLOT_CHEST);
        Eq(c, "C_TOWER", INVENTORY_SLOT_LEFTHAND);
    }
    else if (n == 25) { Eq(c, "C_LONG", INVENTORY_SLOT_RIGHTHAND); }      // elves: longsword proficiency
    else if (n == 26) { Eq(c, "C_SHORTBOW", INVENTORY_SLOT_RIGHTHAND); }  // ...and shortbows
}

void Run(int n)
{
    Step(n);
    DelayCommand(0.6, PLog("eq." + IntToString(n) + ".a", Slots(GetObjectByTag("EQA"))));
    DelayCommand(0.6, PLog("eq." + IntToString(n) + ".b", Slots(GetObjectByTag("EQB"))));
    DelayCommand(0.6, PLog("eq." + IntToString(n) + ".c", Slots(GetObjectByTag("EQC"))));
    if (n < 26) DelayCommand(0.8, Run(n + 1));
    else DelayCommand(1.2, PDone());
}

void main()
{
    CreateObject(OBJECT_TYPE_CREATURE, "nw_hen_sha_09", PCentre(0.0, 0.0), FALSE, "EQA");
    CreateObject(OBJECT_TYPE_CREATURE, "nw_hen_bod_01", PCentre(3.0, 0.0), FALSE, "EQB");
    CreateObject(OBJECT_TYPE_CREATURE, "nw_hen_lin_07", PCentre(6.0, 0.0), FALSE, "EQC");
    DelayCommand(1.5, Run(0));
}
