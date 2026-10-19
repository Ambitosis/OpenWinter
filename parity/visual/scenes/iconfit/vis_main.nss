#include "visual_inc"

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    object item = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(item))
    {
        DestroyObject(item);
        item = GetNextItemInInventory(pc);
    }
    CreateItemOnObject("nw_it_torch001", pc);
    CreateItemOnObject("nw_aarcl001", pc);
    CreateItemOnObject("nw_wswdg001", pc);
    CreateItemOnObject("nw_it_mpotion001", pc, 3);
    DelayCommand(3.0, VCamera(pc, 90.0, 9.0, 45.0));
    DelayCommand(5.0, WriteTimestampedLogEntry("VISUAL|key|73|tap"));
    DelayCommand(7.0, VShot("a_inventory"));
    DelayCommand(9.0, VDone());
}
