// Skills package test: creature OnDisturbed after a noticed pick pocket.
void main()
{
    SetLocalObject(OBJECT_SELF, "disturber", GetLastDisturbed());
    SetLocalInt(OBJECT_SELF, "disturb_type", GetInventoryDisturbType());
    SetLocalObject(OBJECT_SELF, "disturb_item", GetInventoryDisturbItem());
    SetLocalInt(OBJECT_SELF, "disturb_events", GetLocalInt(OBJECT_SELF, "disturb_events") + 1);
}
