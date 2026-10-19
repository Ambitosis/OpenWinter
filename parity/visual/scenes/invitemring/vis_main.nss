#include "visual_inc"

void Profile(object pc)
{
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    object item = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(item))
    {
        int base = GetBaseItemType(item);
        if (base == BASE_ITEM_POTIONS) SetName(item, "Five Potions");
        if (base == BASE_ITEM_DAGGER) SetName(item, "Backpack Dagger");
        if (base == BASE_ITEM_ARMOR) SetName(item, "Backpack Armor");
        WriteTimestampedLogEntry("VISUAL|note|inventory|" + GetName(item)
            + "|base=" + IntToString(base) + "|stack=" + IntToString(GetItemStackSize(item)));
        item = GetNextItemInInventory(pc);
    }
    object robe = GetItemInSlot(INVENTORY_SLOT_CHEST, pc);
    SetName(robe, "Equipped Robe");
    WriteTimestampedLogEntry("VISUAL|note|equipped|valid=" + IntToString(GetIsObjectValid(robe))
        + "|base=" + IntToString(GetBaseItemType(robe)));
}

void Root(float t, string name, string point)
{
    DelayCommand(t, WriteTimestampedLogEntry("VISUAL|hover|" + point));
    DelayCommand(t + .3, WriteTimestampedLogEntry("VISUAL|mouse|" + point + "|down|right"));
    DelayCommand(t + 1.0, VShot(name + "_root"));
    DelayCommand(t + 1.6, WriteTimestampedLogEntry("VISUAL|key|101|tap"));
    DelayCommand(t + 2.0, WriteTimestampedLogEntry("VISUAL|mouse|" + point + "|up|right"));
    DelayCommand(t + 2.7, VShot(name + "_closed"));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    DelayCommand(.5, Profile(pc));
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(3.0, WriteTimestampedLogEntry("VISUAL|key|73|tap"));
    Root(4.0, "potion", "0.1953,0.532");
    Root(8.0, "dagger", "0.0156,0.556");
    Root(12.0, "armor", "0.0547,0.510");
    Root(16.0, "equipped", "0.094,0.094");
    DelayCommand(20.0, VDone());
}
