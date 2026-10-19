// Skills package test: door/placeable OnUnlock.
void main()
{
    object oMod = GetModule();
    SetLocalObject(oMod, "unlocked_by", GetLastUnlocked());
    SetLocalInt(oMod, "unlock_events", GetLocalInt(oMod, "unlock_events") + 1);
}
