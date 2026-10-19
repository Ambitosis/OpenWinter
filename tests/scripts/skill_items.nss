// Skills package test: OBJECT_SELF gets an item by resref (into its inventory, or on the ground unidentified).
void main()
{
    string sResRef = GetLocalString(OBJECT_SELF, "resref");
    object oItem;
    if (GetLocalInt(OBJECT_SELF, "on_ground"))
    {
        oItem = CreateObject(OBJECT_TYPE_ITEM, sResRef, GetLocation(OBJECT_SELF));
        SetIdentified(oItem, FALSE);
    }
    else
        oItem = CreateItemOnObject(sResRef, OBJECT_SELF, GetLocalInt(OBJECT_SELF, "stack") > 0 ? GetLocalInt(OBJECT_SELF, "stack") : 1);
    SetLocalObject(OBJECT_SELF, "made", oItem);
    SetLocalInt(OBJECT_SELF, "made_identified", GetIdentified(oItem));
}
