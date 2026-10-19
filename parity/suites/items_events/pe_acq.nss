#include "parity_itemev"
void main()
{
    object it = GetModuleItemAcquired();
    object by = GetModuleItemAcquiredBy();
    PEvAdd("acq:" + PEvName(it) + ">" + PEvName(by) + "<" + PEvName(GetModuleItemAcquiredFrom()) + "#" + IntToString(GetModuleItemAcquiredStackSize())
        + "@" + PEvName(GetItemPossessor(it)) + "s" + IntToString(PEvSlot(it, by)) + (OBJECT_SELF == GetModule() ? "" : "!self=" + PEvName(OBJECT_SELF)));
}
