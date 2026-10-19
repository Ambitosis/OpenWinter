float RCNow(){return IntToFloat(GetTimeHour()*3600000+GetTimeMinute()*60000+GetTimeSecond()*1000+GetTimeMillisecond())/1000.0;}
void RCState(object target,string stage)
{
    string fx="";int count=0;effect e=GetFirstEffect(target);
    while(GetIsEffectValid(e))
    {
        fx+=GetEffectTag(e)+":"+ObjectToString(GetEffectCreator(e))+":"+IntToString(GetEffectSpellId(e))+":"+IntToString(GetEffectDurationType(e))+":"+IntToString(GetEffectSubType(e))+",";
        count++;e=GetNextEffect(target);
    }
    WriteTimestampedLogEntry("VISUAL|note|restcreatorrules|stage="+stage+"|actor="+GetTag(target)+"|id="+ObjectToString(target)
        +"|pc="+IntToString(GetIsPC(target))+"|resting="+IntToString(GetIsResting(target))+"|action="+IntToString(GetCurrentAction(target))
        +"|count="+IntToString(count)+"|effects_tag_creator_spell_duration_subtype="+fx+"|elapsed="+FloatToString(RCNow()-GetLocalFloat(GetModule(),"start"),0,3));
}
void RCBoth(object pc,string stage){RCState(pc,stage);RCState(GetLocalObject(pc,"control"),stage);}
void RCKit(object target,string prefix)
{
    int marked,duration,sub;
    for(marked=0;marked<2;marked++)for(duration=0;duration<2;duration++)for(sub=0;sub<4;sub++)
    {
        effect e=EffectSkillIncrease(SKILL_SPOT,1);
        if(sub==1)e=SupernaturalEffect(e);else if(sub==2)e=ExtraordinaryEffect(e);else if(sub==3)e=UnyieldingEffect(e);
        if(marked)e=SetEffectSpellId(e,450);
        e=TagEffect(e,prefix+IntToString(marked)+IntToString(duration)+IntToString(sub));
        ApplyEffectToObject(duration==0?DURATION_TYPE_TEMPORARY:DURATION_TYPE_PERMANENT,e,target,300.0);
    }
}
void RCClear(object target){effect e=GetFirstEffect(target);while(GetIsEffectValid(e)){RemoveEffect(target,e);e=GetFirstEffect(target);}}
void RCSetup(object pc)
{
    object control=GetLocalObject(pc,"control");RCClear(pc);RCClear(control);
    RCKit(pc,"m");RCKit(control,"m");
    AssignCommand(pc,RCKit(pc,"p"));AssignCommand(pc,RCKit(control,"p"));
    AssignCommand(control,RCKit(pc,"n"));AssignCommand(control,RCKit(control,"n"));
}
