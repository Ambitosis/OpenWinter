// Placeable OnInvDisturbed for ItemsTests.
void main()
{
    object m = GetModule();
    SetLocalInt(m, "dist", GetLocalInt(m, "dist") + 1);
    SetLocalObject(m, "dist_by", GetLastDisturbed());
    SetLocalObject(m, "dist_item", GetInventoryDisturbItem());
    SetLocalInt(m, "dist_type", GetInventoryDisturbType());
}
