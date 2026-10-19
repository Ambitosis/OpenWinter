// Target OnPhysicalAttacked: counts attacks on it by group.
void main()
{
    object m = GetModule();
    string g = GetLocalString(OBJECT_SELF, "G");
    SetLocalInt(m, g + ".atk", GetLocalInt(m, g + ".atk") + 1);
}
