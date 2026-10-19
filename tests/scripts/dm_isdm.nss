// DM tests: records what scripts see about OBJECT_SELF on the module.
void main()
{
    object oModule = GetModule();
    SetLocalInt(oModule, "dm_isdm", GetIsDM(OBJECT_SELF));
    SetLocalInt(oModule, "dm_ispossessed", GetIsDMPossessed(OBJECT_SELF));
    SetLocalInt(oModule, "dm_ispc", GetIsPC(OBJECT_SELF));
}
