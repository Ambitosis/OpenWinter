// Area OnEnter: the areas a walker goes through, in order.
void main()
{
    object o = GetEnteringObject();
    if (GetLocalInt(o, "RT")) SetLocalString(o, "ROUTE", GetLocalString(o, "ROUTE") + GetTag(OBJECT_SELF) + " ");
}
