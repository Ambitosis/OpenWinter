// Shared readings for the dying scene: real seconds since the scene began and one state line.
float DyNow(){return IntToFloat(GetTimeHour()*3600000+GetTimeMinute()*60000+GetTimeSecond()*1000+GetTimeMillisecond())/1000.0;}
float DyT(){return DyNow()-GetLocalFloat(GetModule(),"t0");}
string DyEffects(object pc)
{
    string s="";effect e=GetFirstEffect(pc);
    while(GetIsEffectValid(e)){s+=IntToString(GetEffectType(e))+"/"+IntToString(GetEffectDurationType(e))+",";e=GetNextEffect(pc);}
    return s;
}
void DyNote(object pc,string tag)
{
    vector v=GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|dy|"+tag+"|t="+FloatToString(DyT(),0,2)+"|hp="+IntToString(GetCurrentHitPoints(pc))
        +"|max="+IntToString(GetMaxHitPoints(pc))+"|dead="+IntToString(GetIsDead(pc))+"|act="+IntToString(GetCurrentAction(pc))
        +"|cmd="+IntToString(GetCommandable(pc))+"|rest="+IntToString(GetIsResting(pc))+"|combat="+IntToString(GetIsInCombat(pc))
        +"|xp="+IntToString(GetXP(pc))+"|gold="+IntToString(GetGold(pc))+"|pos="+FloatToString(v.x,0,2)+","+FloatToString(v.y,0,2)
        +"|fx="+DyEffects(pc));
}
