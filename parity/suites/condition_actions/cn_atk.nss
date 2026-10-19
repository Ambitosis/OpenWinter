// Target OnPhysicalAttacked: counts attacks for the target's group.
void main()
{
    object m = GetModule();
    string g = GetLocalString(OBJECT_SELF, "G");
    SetLocalInt(m, g + ".atk", GetLocalInt(m, g + ".atk") + 1);
}
