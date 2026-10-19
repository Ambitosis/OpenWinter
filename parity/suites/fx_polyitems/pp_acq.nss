void main()
{
    object o = GetModuleItemAcquiredBy();
    string k = "ev." + GetTag(o);
    SetLocalString(GetModule(), k, GetLocalString(GetModule(), k) + " acq:" + GetResRef(GetModuleItemAcquired()));
}
