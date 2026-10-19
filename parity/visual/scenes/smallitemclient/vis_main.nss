#include "visual_inc"

void Item(object pc, string resref, string name, float x)
{
    object item = CreateObject(OBJECT_TYPE_ITEM, resref,
        Location(GetArea(pc), Vector(x, 15.0, 0.0), 0.0));
    SetName(item, name);
    SetLocalObject(pc, name, item);
    WriteTimestampedLogEntry("VISUAL|note|item|" + name
        + "|valid=" + IntToString(GetIsObjectValid(item))
        + "|resref=" + GetResRef(item)
        + "|base=" + IntToString(GetBaseItemType(item)));
}

void Report(object pc, string name)
{
    object item = GetLocalObject(pc, name);
    WriteTimestampedLogEntry("VISUAL|note|pickup|" + name
        + "|valid=" + IntToString(GetIsObjectValid(item))
        + "|owned=" + IntToString(GetItemPossessor(item) == pc)
        + "|arrows=" + IntToString(GetItemInSlot(INVENTORY_SLOT_ARROWS, pc) == item));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetEventScript(GetModule(), EVENT_SCRIPT_MODULE_ON_ACQUIRE_ITEM, "vis_acq");
    WriteTimestampedLogEntry("VISUAL|note|profile|pc=" + IntToString(GetIsPC(pc)));
    // Avoid a same-blueprint stack merge obscuring the original item's ownership.
    object prior = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(prior))
    {
        if (GetBaseItemType(prior) == BASE_ITEM_POTIONS || GetBaseItemType(prior) == BASE_ITEM_ARROW)
            DestroyObject(prior);
        prior = GetNextItemInInventory(pc);
    }
    DestroyObject(GetItemInSlot(INVENTORY_SLOT_ARROWS, pc));
    DelayCommand(.5, Item(pc, "nw_it_mpotion001", "Potion Target", 23.0));
    DelayCommand(.5, Item(pc, "nw_wamar001", "Arrow Target", 17.0));
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(35.0, VDone());
}
