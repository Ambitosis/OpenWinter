float RPNow(){return IntToFloat(GetTimeHour()*3600000+GetTimeMinute()*60000+GetTimeSecond()*1000+GetTimeMillisecond())/1000.0;}
int RPCount(object c)
{
    int n=0;effect e=GetFirstEffect(c);
    while(GetIsEffectValid(e)){n++;e=GetNextEffect(c);}return n;
}
void RPState(object c,string stage)
{
    vector p=GetPosition(c);
    WriteTimestampedLogEntry("VISUAL|note|rest|stage="+stage+"|actor="+GetTag(c)+"|pc="+IntToString(GetIsPC(c))
        +"|resting="+IntToString(GetIsResting(c))+"|effects="+IntToString(RPCount(c))+"|ac="+IntToString(GetAC(c))
        +"|completed="+IntToString(GetLocalInt(c,"completed"))+"|elapsed="+FloatToString(RPNow()-GetLocalFloat(c,"start"),0,3)
        +"|x="+FloatToString(p.x,0,2)+"|y="+FloatToString(p.y,0,2));
}
