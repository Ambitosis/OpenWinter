// Module OnAcquireItem for ItemsTests: records the event data on the module.
void main()
{
    object m = GetModule();
    object oItem = GetModuleItemAcquired();
    SetLocalInt(m, "acq", GetLocalInt(m, "acq") + 1);
    SetLocalObject(m, "acq_item", oItem);
    SetLocalObject(m, "acq_by", GetModuleItemAcquiredBy());
    SetLocalObject(m, "acq_from", GetModuleItemAcquiredFrom());
    SetLocalInt(m, "acq_stack", GetModuleItemAcquiredStackSize());
    string sTag = GetTag(oItem);
    // "marked" is set by the creating script after CreateItemOnObject: the event runs after it finishes.
    // (The modified copy shares the tag; DestroyObject is queued, so it is still valid here.)
    if (sTag == "my_sword" && oItem == GetLocalObject(m, "sword")) SetLocalInt(m, "acq_sword_marked", GetLocalInt(oItem, "marked"));
    if (sTag == "NW_WAMAR001") SetLocalInt(m, "acq_arrows", GetLocalInt(m, "acq_arrows") + GetModuleItemAcquiredStackSize());
}
