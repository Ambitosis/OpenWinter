#include "visual_inc"

void Profile(object pc)
{
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    object item = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(item))
    {
        WriteTimestampedLogEntry("VISUAL|note|inventory|" + GetResRef(item)
            + "|base=" + IntToString(GetBaseItemType(item))
            + "|stack=" + IntToString(GetItemStackSize(item)));
        item = GetNextItemInInventory(pc);
    }
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    DelayCommand(.5, Profile(pc));
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(3.0, WriteTimestampedLogEntry("VISUAL|key|73|tap"));
    DelayCommand(4.5, VShot("inventory"));
    DelayCommand(8.0, VDone());
}
