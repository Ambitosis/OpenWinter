// Counts OnEndCombatRound events per creature tag on the module ("rounds_<tag>").
void main()
{
    object oModule = GetModule();
    string sKey = "rounds_" + GetTag(OBJECT_SELF);
    SetLocalInt(oModule, sKey, GetLocalInt(oModule, sKey) + 1);
}
