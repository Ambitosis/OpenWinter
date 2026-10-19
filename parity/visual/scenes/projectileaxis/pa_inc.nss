float PANow()
{
    int minutes=FloatToInt(HoursToSeconds(1))/60;
    return IntToFloat((GetTimeHour()*minutes+GetTimeMinute())*60+GetTimeSecond())+IntToFloat(GetTimeMillisecond())/1000.0;
}
void PANote(string s)
{
    WriteTimestampedLogEntry("VISUAL|note|projectileaxis|"+s);
}
