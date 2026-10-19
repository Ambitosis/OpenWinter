// Module OnPlayerRest recorder for SpellTests.
void main()
{
    object oM = GetModule();
    string sEvents = GetLocalString(oM, "rest_events");
    SetLocalString(oM, "rest_events", sEvents + IntToString(GetLastRestEventType()));
    SetLocalObject(oM, "rest_pc", GetLastPCRested());
    SetLocalInt(oM, "rest_resting", GetIsResting(GetLastPCRested()));
}
