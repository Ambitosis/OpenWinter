#include "visual_inc"
void State(object pc, string name)
{
    object potion = GetLocalObject(pc, "potion");
    object dagger = GetLocalObject(pc, "dagger");
    object robe = GetLocalObject(pc, "robe");
    WriteTimestampedLogEntry("VISUAL|note|state|" + name + "|potion=" + IntToString(GetItemStackSize(potion))
        + "|potionOwned=" + IntToString(GetItemPossessor(potion) == pc)
        + "|left=" + IntToString(GetItemInSlot(INVENTORY_SLOT_LEFTHAND, pc) == dagger)
        + "|right=" + IntToString(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, pc) == dagger)
        + "|chest=" + IntToString(GetItemInSlot(INVENTORY_SLOT_CHEST, pc) == robe));
}
void Profile(object pc)
{
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    object item = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(item))
    {
        if (GetBaseItemType(item) == BASE_ITEM_POTIONS) SetLocalObject(pc, "potion", item);
        if (GetBaseItemType(item) == BASE_ITEM_DAGGER) SetLocalObject(pc, "dagger", item);
        item = GetNextItemInInventory(pc);
    }
    SetLocalObject(pc, "robe", GetItemInSlot(INVENTORY_SLOT_CHEST, pc));
    State(pc, "ready");
}
void Select(float t, string name, string point, string hover, int key)
{
    DelayCommand(t, WriteTimestampedLogEntry("VISUAL|hover|" + point));
    DelayCommand(t + .2, WriteTimestampedLogEntry("VISUAL|mouse|" + point + "|down|right"));
    DelayCommand(t + .7, WriteTimestampedLogEntry("VISUAL|hover|" + hover));
    DelayCommand(t + 1.4, VShot(name + "_hover"));
    DelayCommand(t + 1.7, WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"));
    DelayCommand(t + 2.0, WriteTimestampedLogEntry("VISUAL|mouse|" + hover + "|up|right"));
}
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    DelayCommand(.5, Profile(pc));
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(3.0, WriteTimestampedLogEntry("VISUAL|key|73|tap"));
    Select(4.0, "drink", "0.1953,0.532", "0.250,0.629", 99);
    DelayCommand(8.5, State(pc, "drank"));
    Select(9.0, "offhand", "0.0156,0.556", "0.0422,0.556", 100);
    DelayCommand(12.0, State(pc, "equipped"));
    Select(13.0, "unequip", "0.094,0.094", "0.0422,0.214", 100);
    DelayCommand(16.0, State(pc, "unequipped"));
    Select(17.0, "drop", "0.1953,0.532", "0.1953,0.671", 98);
    DelayCommand(21.0, State(pc, "dropped"));
    DelayCommand(21.2, VShot("final"));
    DelayCommand(23.0, VDone());
}
