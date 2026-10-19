// Standing still on arrival in each area of an inter-area route, and the pause before each leg of a move-away
// (expected: about 0.75 + 0.3 s in an intermediate area, 0.75 s in the final one; 0.3 s
// before each leg away). A walker in RAA is ordered ActionMoveToObject to a waypoint in RAC, through RAB (one
// transition each, 20 m walks between them). Its area and position are sampled every 0.05 s; per area: when it appeared
// (first sample there) and how long it stood at its arrival point. Three walkers, 30 s apart.
// Move-away: a stripped bandit 2 m from another is ordered ActionMoveAwayFromObject(other, FALSE, 10.0); the stand
// before its first step and every later stop of 0.15 s or more in the first 10 s.
#include "parity_inc"
#include "parity_time"
void Sample(object o,int n)
{
    object a=GetArea(o);vector p=GetPosition(o);string at=GetTag(a);
    if(at!=GetLocalString(o,"A")){SetLocalString(o,"A",at);SetLocalFloat(o,"t0_"+at,PSince());SetLocalLocation(o,"p0_"+at,GetLocation(o));SetLocalInt(o,"moved_"+at,0);}
    else if(!GetLocalInt(o,"moved_"+at)&&VectorMagnitude(p-GetPositionFromLocation(GetLocalLocation(o,"p0_"+at)))>0.05)
    {SetLocalInt(o,"moved_"+at,1);SetLocalFloat(o,"stand_"+at,PSince()-GetLocalFloat(o,"t0_"+at));}
    if(n>0)DelayCommand(0.05,Sample(o,n-1));
}
void Walker(int i)
{
    object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",GetLocation(GetWaypointByTag("RA_START")),FALSE,"W"+IntToString(i));
    int e;for(e=5000;e<=5012;e++)SetEventScript(o,e,"");ChangeToStandardFaction(o,STANDARD_FACTION_MERCHANT);SetAILevel(o,AI_LEVEL_NORMAL);
    SetLocalInt(o,"RA",1);
    DelayCommand(4.0,AssignCommand(o,ActionMoveToObject(GetWaypointByTag("RC_GOAL"))));
    DelayCommand(4.0,Sample(o,700));
}
void Report(int i)
{
    object o=GetObjectByTag("W"+IntToString(i));string k="w"+IntToString(i);
    WriteTimestampedLogEntry("RAW|"+k+"|enter="+GetLocalString(o,"ENTER")+" tB="+FloatToString(GetLocalFloat(o,"t0_RAB"),0,2)+" tC="+FloatToString(GetLocalFloat(o,"t0_RAC"),0,2)
        +" standA="+FloatToString(GetLocalFloat(o,"stand_RAA"),0,2)+" standB="+FloatToString(GetLocalFloat(o,"stand_RAB"),0,2)+" standC="+FloatToString(GetLocalFloat(o,"stand_RAC"),0,2)
        +" end="+GetTag(GetArea(o))+" goal="+FloatToString(GetDistanceBetween(o,GetWaypointByTag("RC_GOAL")),0,2));
    PLog(k+".area",GetTag(GetArea(o)));
    PLogF(k+".standB",GetLocalFloat(o,"stand_RAB"),0.25);PLogF(k+".standC",GetLocalFloat(o,"stand_RAC"),0.25);
}
// Move-away: stops of 0.15 s or more.
void Track(object o,vector prev,float fStill,int n)
{
    vector p=GetPosition(o);float t=PSince();
    if(VectorMagnitude(p-prev)>0.01)
    {
        if(fStill>=0.0&&t-fStill>=0.15)SetLocalString(o,"STOPS",GetLocalString(o,"STOPS")+FloatToString(fStill-GetLocalFloat(o,"T0"),0,2)+"+"+FloatToString(t-fStill,0,2)+" ");
        fStill=-1.0;
    }
    else if(fStill<0.0)fStill=t;
    if(n>0)DelayCommand(0.05,Track(o,p,fStill,n-1));
}
void Away(int i)
{
    object a=GetObjectByTag("RAD");
    object b=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(a,Vector(10.0,8.0+IntToFloat(i)*8.0,0.0),0.0),FALSE,"B"+IntToString(i));
    object m=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(a,Vector(12.0,8.0+IntToFloat(i)*8.0,0.0),0.0),FALSE,"M"+IntToString(i));
    int e;for(e=5000;e<=5012;e++){SetEventScript(b,e,"");SetEventScript(m,e,"");}
    ChangeToStandardFaction(b,STANDARD_FACTION_MERCHANT);ChangeToStandardFaction(m,STANDARD_FACTION_MERCHANT);SetAILevel(m,AI_LEVEL_NORMAL);
    DelayCommand(8.0,SetLocalFloat(m,"T0",PSince()));
    DelayCommand(8.0,AssignCommand(m,ActionMoveAwayFromObject(b,FALSE,10.0)));
    DelayCommand(8.0,Track(m,GetPosition(m),PSince()+8.0,200));
}
void AwayReport(int i)
{
    object m=GetObjectByTag("M"+IntToString(i));
    WriteTimestampedLogEntry("RAW|away"+IntToString(i)+"|stops="+GetLocalString(m,"STOPS")+" dist="+FloatToString(GetDistanceBetween(m,GetObjectByTag("B"+IntToString(i))),0,2));
    PLog("away"+IntToString(i)+".stops",GetLocalString(m,"STOPS")+"|~");
    PLogF("away"+IntToString(i)+".dist",GetDistanceBetween(m,GetObjectByTag("B"+IntToString(i))),0.5);
}
void main()
{
    PStart();int i;
    for(i=0;i<3;i++)DelayCommand(IntToFloat(i)*30.0,Walker(i));
    for(i=0;i<3;i++)Away(i);
    for(i=0;i<3;i++){DelayCommand(110.0,Report(i));DelayCommand(110.0,AwayReport(i));}
    DelayCommand(111.0,PDone());
}
