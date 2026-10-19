// Module OnPlayerUnEquipItem for ItemsTests.
void main()
{
    object m = GetModule();
    SetLocalInt(m, "unequ", GetLocalInt(m, "unequ") + 1);
    SetLocalObject(m, "unequ_item", GetPCItemLastUnequipped());
    SetLocalObject(m, "unequ_by", GetPCItemLastUnequippedBy());
    SetLocalInt(m, "unequ_slot", GetPCItemLastUnequippedSlot());
}
