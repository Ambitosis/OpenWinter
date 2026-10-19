#include "visual_inc"

void Profile(object pc)
{
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    SetMemorizedSpell(pc, CLASS_TYPE_WIZARD, 1, 0, SPELL_MAGIC_WEAPON, TRUE);
    WriteTimestampedLogEntry("VISUAL|note|prepared|wizard=" + IntToString(GetLevelByClass(CLASS_TYPE_WIZARD, pc))
        + "|spell=" + IntToString(GetMemorizedSpellId(pc, CLASS_TYPE_WIZARD, 1, 0))
        + "|ready=" + IntToString(GetMemorizedSpellReady(pc, CLASS_TYPE_WIZARD, 1, 0)));
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

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    DelayCommand(.5, Profile(pc));
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(85.0, VDone());
}
