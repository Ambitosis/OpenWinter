void main()
{
    object pc = GetLastClosedBy(); vector p = GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|store_closed|pc=" + IntToString(GetIsPC(pc))
        + "|x=" + FloatToString(p.x) + "|y=" + FloatToString(p.y));
}
