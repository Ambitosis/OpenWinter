// Run on a creature next to door "WORLD_DOOR": locks it with a key and queues an open.
void main()
{
    object m = GetModule();
    object oDoor = GetObjectByTag("WORLD_DOOR");
    SetLocked(oDoor, TRUE);
    SetLockKeyRequired(oDoor, TRUE);
    SetLockKeyTag(oDoor, "world_key");
    SetLockLockable(oDoor, TRUE);
    SetLockUnlockDC(oDoor, 300);
    SetHardness(7, oDoor);
    SetLocalInt(m, "locked", GetLocked(oDoor));
    SetLocalInt(m, "key_required", GetLockKeyRequired(oDoor));
    SetLocalString(m, "key_tag", GetLockKeyTag(oDoor));
    SetLocalInt(m, "unlock_dc", GetLockUnlockDC(oDoor));
    SetLocalInt(m, "hardness", GetHardness(oDoor));
    SetLocalInt(m, "open_possible", GetIsDoorActionPossible(oDoor, DOOR_ACTION_OPEN));
    SetLocalInt(m, "unlock_possible", GetIsDoorActionPossible(oDoor, DOOR_ACTION_UNLOCK));
    SetLocalInt(m, "is_open", GetIsOpen(oDoor));
    SetLocalInt(m, "hardness_creature", GetHardness(OBJECT_SELF));
    ActionOpenDoor(oDoor);
}
