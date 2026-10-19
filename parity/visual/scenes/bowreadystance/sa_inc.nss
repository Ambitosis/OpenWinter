float SANow()
{
    int minutes=FloatToInt(HoursToSeconds(1))/60;
    return IntToFloat((GetTimeHour()*minutes+GetTimeMinute())*60+GetTimeSecond())+IntToFloat(GetTimeMillisecond())/1000.0;
}
void SANote(string s)
{
    WriteTimestampedLogEntry("VISUAL|note|seekerarrow|t="+FloatToString(SANow()-GetLocalFloat(GetModule(),"sa_start"),0,3)+"|"+s);
}
