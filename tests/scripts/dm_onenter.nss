// DM tests: module OnClientEnter records whether the entering object is a DM.
void main()
{
    object oEntering = GetEnteringObject();
    SetLocalInt(GetModule(), "dm_enter_count", GetLocalInt(GetModule(), "dm_enter_count") + 1);
    if (GetIsDM(oEntering)) SetLocalInt(GetModule(), "dm_entered", 1);
}
