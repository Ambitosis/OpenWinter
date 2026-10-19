float RRNow(){return IntToFloat(GetTimeHour()*3600000+GetTimeMinute()*60000+GetTimeSecond()*1000+GetTimeMillisecond())/1000.0;}
void RRState(object pc,string stage)
{
    string fx="";int count=0;effect e=GetFirstEffect(pc);
    while(GetIsEffectValid(e))
    {
        fx+=GetEffectTag(e)+":"+IntToString(GetEffectSpellId(e))+":"+IntToString(GetEffectDurationType(e))+":"+IntToString(GetEffectSubType(e))+",";
        count++;e=GetNextEffect(pc);
    }
    WriteTimestampedLogEntry("VISUAL|note|restspellrules|stage="+stage+"|trial="+IntToString(GetLocalInt(pc,"trial"))
        +"|valid="+IntToString(GetIsObjectValid(pc))+"|pc="+IntToString(GetIsPC(pc))
        +"|resting="+IntToString(GetIsResting(pc))+"|action="+IntToString(GetCurrentAction(pc))
        +"|completed="+IntToString(GetLocalInt(pc,"completed"))+"|rested="+IntToString(GetLocalInt(pc,"rested"))
        +"|no_rest="+IntToString(GetAreaNoRestFlag(GetArea(pc)))+"|count="+IntToString(count)+"|effects="+fx
        +"|elapsed="+FloatToString(RRNow()-GetLocalFloat(pc,"start"),0,3));
}
void RRKit(object pc,int trial)
{
    effect old=GetFirstEffect(pc);while(GetIsEffectValid(old)){RemoveEffect(pc,old);old=GetFirstEffect(pc);}
    SetLocalInt(pc,"trial",trial);SetLocalInt(pc,"completed",0);SetLocalInt(pc,"rested",0);
    int marked,duration,sub;
    for(marked=0;marked<2;marked++)for(duration=0;duration<2;duration++)for(sub=0;sub<4;sub++)
    {
        effect e=EffectSkillIncrease(SKILL_SPOT,1);
        if(sub==1)e=SupernaturalEffect(e);else if(sub==2)e=ExtraordinaryEffect(e);else if(sub==3)e=UnyieldingEffect(e);
        if(marked)e=SetEffectSpellId(e,450);
        e=TagEffect(e,IntToString(marked)+IntToString(duration)+IntToString(sub));
        ApplyEffectToObject(duration==0?DURATION_TYPE_TEMPORARY:DURATION_TYPE_PERMANENT,e,pc,300.0);
    }
    RRState(pc,"kit");
}
