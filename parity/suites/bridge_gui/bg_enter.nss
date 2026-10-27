// The entering player gets the GUI event script and, once in the area, two creatures and a chest beside it and an
// item to examine.
void Place(object oPC)
{
    vector v = GetPosition(oPC);
    object oArea = GetArea(oPC);
    if (!GetIsObjectValid(GetObjectByTag("BG_NPC")))
    {
        object oNpc = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", Location(oArea, Vector(v.x + 3.0, v.y, v.z), 180.0), FALSE, "BG_NPC");
        SetName(oNpc, "Npc One");
        object oNpc2 = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", Location(oArea, Vector(v.x + 3.0, v.y + 3.0, v.z), 180.0), FALSE, "BG_NPC2");
        SetName(oNpc2, "Npc Two");
        object oChest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", Location(oArea, Vector(v.x - 2.0, v.y + 3.0, v.z), 0.0), FALSE, "BG_CHEST");
        SetName(oChest, "Test Chest");
    }
    if (!GetIsObjectValid(GetItemPossessedBy(oPC, "BG_ITEM")))
        CreateItemOnObject("nw_it_mpotion001", oPC, 1, "BG_ITEM");
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetEventScript(GetModule(), EVENT_SCRIPT_MODULE_ON_PLAYER_GUIEVENT, "bg_gui");
    DelayCommand(2.0, Place(oPC));
    WriteTimestampedLogEntry("GUIEV|enter|" + GetName(oPC));
}
