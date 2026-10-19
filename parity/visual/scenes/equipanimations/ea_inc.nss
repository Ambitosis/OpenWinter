float EANow()
{
    int minutes=FloatToInt(HoursToSeconds(1))/60;
    return IntToFloat((GetTimeHour()*minutes+GetTimeMinute())*60+GetTimeSecond())+IntToFloat(GetTimeMillisecond())/1000.0;
}
void EANote(string s){WriteTimestampedLogEntry("VISUAL|note|equipanimations|"+s);}
string EAItem(object item)
{
    if(!GetIsObjectValid(item))return "none";
    return GetTag(item)+":"+GetResRef(item);
}
void EAState(object c,string stage)
{
    vector p=GetPosition(c);
    object club=GetLocalObject(c,"club"),shield=GetLocalObject(c,"shield");
    int phase=GetLocalInt(c,"phase");
    object right=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c),left=GetItemInSlot(INVENTORY_SLOT_LEFTHAND,c);
    int hands=(phase==1&&right==club&&!GetIsObjectValid(left))
        ||(phase==3&&!GetIsObjectValid(right)&&left==shield)
        ||((phase==0||phase==2||phase==4)&&!GetIsObjectValid(right)&&!GetIsObjectValid(left));
    EANote("stage="+stage+"|actor="+GetTag(c)+"|valid="+IntToString(GetIsObjectValid(c))
        +"|appearance="+IntToString(GetAppearanceType(c))+"|pc="+IntToString(GetIsPC(c))
        +"|stationary="+IntToString(GetDistanceBetweenLocations(GetLocation(c),GetLocalLocation(c,"origin"))<.01)
        +"|x="+FloatToString(p.x,0,3)+"|y="+FloatToString(p.y,0,3)+"|facing="+FloatToString(GetFacing(c),0,2)
        +"|phase="+IntToString(phase)+"|hands_match="+IntToString(hands)
        +"|right="+EAItem(right)+"|left="+EAItem(left)
        +"|owns_club="+IntToString(GetItemPossessor(club)==c)+"|owns_shield="+IntToString(GetItemPossessor(shield)==c)
        +"|action="+IntToString(GetCurrentAction(c))+"|completed="+IntToString(GetLocalInt(c,"completed"))
        +"|equip_events="+IntToString(GetLocalInt(c,"equip_events"))+"|unequip_events="+IntToString(GetLocalInt(c,"unequip_events"))
        +"|elapsed="+FloatToString(EANow()-GetLocalFloat(c,"start"),0,3));
}
