void main()
{
    object item = GetModuleItemAcquired();
    object owner = GetModuleItemAcquiredBy();
    WriteTimestampedLogEntry("VISUAL|note|acquire|" + GetName(item)
        + "|resref=" + GetResRef(item)
        + "|pc=" + IntToString(GetIsPC(owner))
        + "|owned=" + IntToString(GetItemPossessor(item) == owner));
}
