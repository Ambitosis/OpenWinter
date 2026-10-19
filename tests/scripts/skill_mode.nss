// Skills package test: OBJECT_SELF sets an action mode, then reports its modes and what it perceives.
void main()
{
    int nMode = GetLocalInt(OBJECT_SELF, "mode");
    if (GetLocalInt(OBJECT_SELF, "mode_set"))
        SetActionMode(OBJECT_SELF, nMode, GetLocalInt(OBJECT_SELF, "mode_on"));
    SetLocalInt(OBJECT_SELF, "mode_now", GetActionMode(OBJECT_SELF, nMode));
    SetLocalInt(OBJECT_SELF, "stealth", GetStealthMode(OBJECT_SELF));
    SetLocalInt(OBJECT_SELF, "detect", GetDetectMode(OBJECT_SELF));
    object oWatch = GetLocalObject(OBJECT_SELF, "watch");
    SetLocalInt(OBJECT_SELF, "seen", GetObjectSeen(oWatch));
    SetLocalInt(OBJECT_SELF, "heard", GetObjectHeard(oWatch));
}
