string VectorToString(vector v)
{
    return FloatToString(v.x,0,3)+","+FloatToString(v.y,0,3)+","+FloatToString(v.z,0,3);
}
float EBNow()
{
    int minutes = FloatToInt(HoursToSeconds(1)) / 60;
    return IntToFloat((GetTimeHour()*minutes+GetTimeMinute())*60+GetTimeSecond())
        + IntToFloat(GetTimeMillisecond())/1000.0;
}
void EBNote(string value)
{
    WriteTimestampedLogEntry("VISUAL|note|extractbrain|t="
        +FloatToString(EBNow()-GetLocalFloat(GetModule(),"eb_start"),0,3)+"|"+value);
}
