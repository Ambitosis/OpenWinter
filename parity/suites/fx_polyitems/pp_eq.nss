void main()
{
    object o = GetPCItemLastEquippedBy();
    string k = "ev." + GetTag(o);
    SetLocalString(GetModule(), k, GetLocalString(GetModule(), k) + " eq:" + GetResRef(GetPCItemLastEquipped()) + "@" + IntToString(GetPCItemLastEquippedSlot()));
}
