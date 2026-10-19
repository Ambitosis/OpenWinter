// Module OnPlayerEquipItem for ItemsTests.
void main()
{
    object m = GetModule();
    SetLocalInt(m, "equ", GetLocalInt(m, "equ") + 1);
    SetLocalObject(m, "equ_item", GetPCItemLastEquipped());
    SetLocalObject(m, "equ_by", GetPCItemLastEquippedBy());
    SetLocalInt(m, "equ_slot", GetPCItemLastEquippedSlot());
    SetLocalInt(m, "equ_inslot", GetItemInSlot(GetPCItemLastEquippedSlot(), GetPCItemLastEquippedBy()) == GetPCItemLastEquipped());
}
