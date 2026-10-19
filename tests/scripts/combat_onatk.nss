// Counts OnPhysicalAttacked events on the module ("attacked").
void main()
{
    object oModule = GetModule();
    SetLocalInt(oModule, "attacked", GetLocalInt(oModule, "attacked") + 1);
}
