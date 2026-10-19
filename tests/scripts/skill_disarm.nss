// Skills package test: trap OnDisarm.
void main()
{
    object oMod = GetModule();
    SetLocalObject(oMod, "disarmed_by", GetLastDisarmed());
    SetLocalInt(oMod, "disarm_events", GetLocalInt(oMod, "disarm_events") + 1);
}
