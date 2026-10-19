// Run on the creature: close and lock the door again.
void main()
{
    object oDoor = GetObjectByTag("WORLD_DOOR");
    ActionCloseDoor(oDoor);
    ActionLockObject(oDoor);
}
