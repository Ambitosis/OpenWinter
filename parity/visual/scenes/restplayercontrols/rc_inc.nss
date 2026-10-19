float RCNow(){return IntToFloat(GetTimeHour()*3600000+GetTimeMinute()*60000+GetTimeSecond()*1000+GetTimeMillisecond())/1000.0;}
void RCState(object pc,string stage)
{
    int n=0;string spells="";effect e=GetFirstEffect(pc);
    while(GetIsEffectValid(e)){n++;spells+=IntToString(GetEffectSpellId(e))+",";e=GetNextEffect(pc);}
    WriteTimestampedLogEntry("VISUAL|note|restpc|stage="+stage+"|trial="+IntToString(GetLocalInt(pc,"trial"))
        +"|resting="+IntToString(GetIsResting(pc))+"|effects="+IntToString(n)+"|spells="+spells+"|ac="+IntToString(GetAC(pc))
        +"|completed="+IntToString(GetLocalInt(pc,"completed"))+"|elapsed="+FloatToString(RCNow()-GetLocalFloat(pc,"start"),0,3));
}
