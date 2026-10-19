void main()
{
    object o = GetPCItemLastUnequippedBy();
    string k = "ev." + GetTag(o);
    SetLocalString(GetModule(), k, GetLocalString(GetModule(), k) + " uneq:" + GetResRef(GetPCItemLastUnequipped()) + "@" + IntToString(GetPCItemLastUnequippedSlot()));
}
