// Public NWScript game clock; seconds advance in real time within a game day.
float Now()
{
    int minutes = FloatToInt(HoursToSeconds(1)) / 60;
    return IntToFloat((GetTimeHour() * minutes + GetTimeMinute()) * 60 + GetTimeSecond())
        + IntToFloat(GetTimeMillisecond()) / 1000.0;
}
void Note(string detail)
{
    object m = GetModule();
    WriteTimestampedLogEntry("VISUAL|note|" + GetLocalString(m,"case") + "|t="
        + FloatToString(Now()-GetLocalFloat(m,"start"),0,3) + "|" + detail);
}
