float RDNow(){return IntToFloat(GetTimeHour()*3600000+GetTimeMinute()*60000+GetTimeSecond()*1000+GetTimeMillisecond())/1000.0;}
void RDState(object c,string stage)
{
    string fx="";int n=0;effect e=GetFirstEffect(c);
    while(GetIsEffectValid(e))
    {
        fx+=IntToString(GetEffectSpellId(e))+":"+IntToString(GetEffectType(e))+":"+IntToString(GetEffectSubType(e))
            +":"+IntToString(GetEffectCasterLevel(e))+":"+IntToString(GetEffectDurationRemaining(e))+",";
        n++;e=GetNextEffect(c);
    }
    WriteTimestampedLogEntry("VISUAL|note|reststockduration|stage="+stage+"|actor="+GetTag(c)+"|valid="+IntToString(GetIsObjectValid(c))
        +"|pc="+IntToString(GetIsPC(c))+"|cleric="+IntToString(GetLevelByClass(CLASS_TYPE_CLERIC,c))
        +"|resting="+IntToString(GetIsResting(c))+"|action="+IntToString(GetCurrentAction(c))+"|combat="+IntToString(GetIsInCombat(c))+"|ac="+IntToString(GetAC(c))
        +"|events="+IntToString(GetLocalInt(c,"spell_events"))+"|last_spell="+IntToString(GetLocalInt(c,"last_spell"))
        +"|count="+IntToString(n)+"|effects_spell_type_subtype_cl_remaining="+fx
        +"|elapsed="+FloatToString(RDNow()-GetLocalFloat(GetModule(),"start"),0,3));
}
