// Records which world-object event fired and who caused it (world package tests).
void Note(string sKey, object oWho)
{
    if (GetIsObjectValid(oWho)) SetLocalObject(OBJECT_SELF, sKey, oWho);
}

void main()
{
    SetLocalInt(OBJECT_SELF, "events", GetLocalInt(OBJECT_SELF, "events") + 1);
    Note("opened", GetLastOpenedBy());
    Note("closed", GetLastClosedBy());
    Note("locked", GetLastLocked());
    Note("unlocked", GetLastUnlocked());
    Note("clicking", GetClickingObject());
    Note("entering", GetEnteringObject());
    Note("exiting", GetExitingObject());
    Note("used", GetLastUsedBy());
    SetLocalInt(OBJECT_SELF, "is_open", GetIsOpen(OBJECT_SELF));
}
