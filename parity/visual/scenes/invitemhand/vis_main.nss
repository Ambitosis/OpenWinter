#include "visual_inc"
void State(object pc, string phase)
{
    WriteTimestampedLogEntry("VISUAL|note|hands|" + phase
        + "|right=" + IntToString(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, pc) == GetLocalObject(pc, "right"))
        + "|left=" + IntToString(GetItemInSlot(INVENTORY_SLOT_LEFTHAND, pc) == GetLocalObject(pc, "left")));
}
void Setup(object pc)
{
    object item = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(item))
    {
        if (GetBaseItemType(item) == BASE_ITEM_DAGGER) SetLocalObject(pc, "left", item);
        item = GetNextItemInInventory(pc);
    }
    object right = CreateItemOnObject("nw_wswdg001", pc);
    SetLocalObject(pc, "right", right);
    AssignCommand(pc, ActionEquipItem(right, INVENTORY_SLOT_RIGHTHAND));
}
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    DelayCommand(.5, Setup(pc));
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(2.5, State(pc, "ready"));
    DelayCommand(3.0, WriteTimestampedLogEntry("VISUAL|key|73|tap"));
    DelayCommand(4.0, WriteTimestampedLogEntry("VISUAL|hover|0.0156,0.556"));
    DelayCommand(4.2, WriteTimestampedLogEntry("VISUAL|mouse|0.0156,0.556|down|right"));
    DelayCommand(4.6, VShot("held_root"));
    DelayCommand(4.9, WriteTimestampedLogEntry("VISUAL|hover|0.1203,0.556"));
    DelayCommand(5.1, WriteTimestampedLogEntry("VISUAL|mouse|0.1203,0.556|up|right"));
    DelayCommand(5.8, VShot("released_center"));
    DelayCommand(6.1, WriteTimestampedLogEntry("VISUAL|hover|0.0156,0.556"));
    DelayCommand(6.3, WriteTimestampedLogEntry("VISUAL|mouse|0.0156,0.556|down|right"));
    DelayCommand(6.7, WriteTimestampedLogEntry("VISUAL|hover|0.0422,0.556"));
    DelayCommand(7.3, VShot("offhand_hover"));
    DelayCommand(7.6, WriteTimestampedLogEntry("VISUAL|key|100|tap"));
    DelayCommand(7.9, WriteTimestampedLogEntry("VISUAL|mouse|0.0422,0.556|up|right"));
    DelayCommand(10.0, State(pc, "selected"));
    DelayCommand(10.2, VShot("equipped"));
    DelayCommand(12.0, VDone());
}
