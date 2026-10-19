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

void Ring(object pc, float t, string name, string x, string east)
{
    DelayCommand(t, WriteTimestampedLogEntry("VISUAL|hover|" + x + ",0.628"));
    DelayCommand(t + .3, WriteTimestampedLogEntry("VISUAL|mouse|" + x + ",0.628|down|right"));
    DelayCommand(t + 1.0, VShot(name + "_root"));
    DelayCommand(t + 1.5, WriteTimestampedLogEntry("VISUAL|hover|" + east + ",0.628"));
    DelayCommand(t + 2.5, VShot(name + "_east"));
    DelayCommand(t + 3.0, WriteTimestampedLogEntry("VISUAL|hover|" + x + ",0.766"));
    DelayCommand(t + 4.0, VShot(name + "_south"));
    DelayCommand(t + 4.5, WriteTimestampedLogEntry("VISUAL|hover|" + x + ",0.628"));
    DelayCommand(t + 5.0, WriteTimestampedLogEntry("VISUAL|key|98|tap"));
    DelayCommand(t + 5.5, WriteTimestampedLogEntry("VISUAL|mouse|" + x + ",0.628|up|right"));
    DelayCommand(t + 8.0, Report(pc, name));
    DelayCommand(t + 8.5, VShot(name + "_after"));
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
    Ring(pc, 4.0, "Potion Target", "0.743", "0.821");
    DelayCommand(14.0, VMovePC(pc, 20.0, 15.0, 0.0, 90.0));
    Ring(pc, 16.0, "Arrow Target", "0.246", "0.324");
    DelayCommand(27.0, VDone());
}
