void main()
{
    int count = GetLocalInt(OBJECT_SELF, "fixture_fails") + 1;
    SetLocalInt(OBJECT_SELF, "fixture_fails", count);
    WriteTimestampedLogEntry("VISUAL|note|fail_hook|count=" + IntToString(count)
        + "|clicker_pc=" + IntToString(GetIsPC(GetClickingObject())));
    ExecuteScript("nw_od_feedback8", OBJECT_SELF);
}
