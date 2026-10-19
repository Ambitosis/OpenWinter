// Public PC event callbacks, separated from polled GetObjectSeen/Heard state.
float CVEventNow()
{
    int minutes=FloatToInt(HoursToSeconds(1))/60;
    return IntToFloat((GetTimeHour()*minutes+GetTimeMinute())*60+GetTimeSecond())
        +IntToFloat(GetTimeMillisecond())/1000.0;
}
void main()
{
    if(GetLocalInt(OBJECT_SELF,"cv_control"))
    {
        WriteTimestampedLogEntry("VISUAL|note|pc_handler|execution_control=1|is_pc="+IntToString(GetIsPC(OBJECT_SELF)));
        return;
    }
    object target=GetLastPerceived();
    if(GetTag(target)!="CV_ACTOR")return;
    WriteTimestampedLogEntry("VISUAL|note|pc_event|stage="+GetLocalString(OBJECT_SELF,"cv_stage")
        +"|t="+FloatToString(CVEventNow()-GetLocalFloat(OBJECT_SELF,"cv_start"),0,3)
        +"|seen_event="+IntToString(GetLastPerceptionSeen())
        +"|heard_event="+IntToString(GetLastPerceptionHeard())
        +"|vanished_event="+IntToString(GetLastPerceptionVanished())
        +"|inaudible_event="+IntToString(GetLastPerceptionInaudible())
        +"|seen="+IntToString(GetObjectSeen(target,OBJECT_SELF))
        +"|heard="+IntToString(GetObjectHeard(target,OBJECT_SELF)));
}
