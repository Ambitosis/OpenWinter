// Module OnActivateItem for ItemsTests.
void main()
{
    object m = GetModule();
    SetLocalInt(m, "act", GetLocalInt(m, "act") + 1);
    SetLocalObject(m, "act_item", GetItemActivated());
    SetLocalObject(m, "act_by", GetItemActivator());
    SetLocalObject(m, "act_target", GetItemActivatedTarget());
}
