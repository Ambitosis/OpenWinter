// Module OnUnAcquireItem for ItemsTests.
void main()
{
    object m = GetModule();
    SetLocalInt(m, "lost", GetLocalInt(m, "lost") + 1);
    SetLocalObject(m, "lost_item", GetModuleItemLost());
    SetLocalObject(m, "lost_by", GetModuleItemLostBy());
}
