void main()
{
    object pc = GetLastClosedBy(); vector p = GetPosition(pc);
    SetLocalInt(OBJECT_SELF, "closed", GetLocalInt(OBJECT_SELF, "closed") + 1);
    WriteTimestampedLogEntry("VISUAL|note|store_closed|pc=" + IntToString(GetIsPC(pc))
        + "|x=" + FloatToString(p.x) + "|y=" + FloatToString(p.y));
}
