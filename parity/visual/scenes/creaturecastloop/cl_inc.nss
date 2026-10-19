string VectorToString(vector v)
{
    return FloatToString(v.x,0,3)+","+FloatToString(v.y,0,3)+","+FloatToString(v.z,0,3);
}
float CLNow()
{
    int minutes=FloatToInt(HoursToSeconds(1))/60;
    return IntToFloat((GetTimeHour()*minutes+GetTimeMinute())*60+GetTimeSecond())+IntToFloat(GetTimeMillisecond())/1000.0;
}
void CLNote(string s){WriteTimestampedLogEntry("VISUAL|note|creaturecastloop|"+s);}
void State(object c,string stage)
{
    CLNote("stage="+stage+"|caster="+GetTag(c)+"|valid="+IntToString(GetIsObjectValid(c))
        +"|appearance="+IntToString(GetAppearanceType(c))+"|rat="+IntToString(GetAppearanceType(c)==APPEARANCE_TYPE_RAT)
        +"|stationary="+IntToString(GetDistanceBetweenLocations(GetLocation(c),GetLocalLocation(c,"origin"))<0.001)
        +"|pos="+VectorToString(GetPosition(c))+"|facing="+FloatToString(GetFacing(c),0,2)
        +"|action="+IntToString(GetCurrentAction(c))+"|impacts="+IntToString(GetLocalInt(c,"impacts"))
        +"|finished="+IntToString(GetLocalInt(c,"finished"))
        +"|elapsed="+FloatToString(CLNow()-GetLocalFloat(c,"start"),0,3));
}
