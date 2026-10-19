void main()
{
    object o = GetModuleItemLostBy();
    string k = "ev." + GetTag(o);
    SetLocalString(GetModule(), k, GetLocalString(GetModule(), k) + " unacq:" + GetResRef(GetModuleItemLost()));
}
