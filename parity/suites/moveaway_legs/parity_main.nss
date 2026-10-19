// The pause before each leg of ActionMoveAwayFromObject (expected: 0.3 s for an NPC before
// each leg, including the first). Stripped bandits 2 m from another are ordered ActionMoveAwayFromObject(other, FALSE,
// fRange) at 10 s; the distance is sampled every 0.05 s for 8 s: the time of the first step, and each later stand.
#include "parity_inc"
#include "parity_time"
void Track(object o,object b,int n)
{
    float d=GetDistanceBetween(o,b);float t=PSince()-GetLocalFloat(o,"T0");
    if(d>GetLocalFloat(o,"D")+0.01)
    {
        if(GetLocalFloat(o,"FIRST")==0.0)SetLocalFloat(o,"FIRST",t);
        float s=GetLocalFloat(o,"STILL");
        if(s>0.0&&t-s>=0.12)SetLocalString(o,"STOPS",GetLocalString(o,"STOPS")+FloatToString(s,0,2)+"+"+FloatToString(t-s,0,2)+" ");
        SetLocalFloat(o,"STILL",0.0);SetLocalFloat(o,"D",d);
    }
    else if(GetLocalFloat(o,"STILL")==0.0&&GetLocalFloat(o,"FIRST")>0.0)SetLocalFloat(o,"STILL",t);
    if(n<5*20)SetLocalString(o,"SEQ",GetLocalString(o,"SEQ")+FloatToString(d,0,2)+" ");
    if(n>0)DelayCommand(0.05,Track(o,b,n-1));
}
void Go(object m,object b,float r)
{
    SetLocalFloat(m,"T0",PSince());SetLocalFloat(m,"D",GetDistanceBetween(m,b));
    AssignCommand(m,ActionMoveAwayFromObject(b,FALSE,r));Track(m,b,160);
}
void Pair(int i,float r)
{
    object a=GetObjectByTag("PARITY_AREA");float y=4.0+IntToFloat(i)*4.0;
    object b=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(a,Vector(4.0,y,0.0),0.0),FALSE,"B"+IntToString(i));
    object m=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(a,Vector(6.0,y,0.0),0.0),FALSE,"M"+IntToString(i));
    int e;for(e=5000;e<=5012;e++){SetEventScript(b,e,"");SetEventScript(m,e,"");}
    ChangeToStandardFaction(b,STANDARD_FACTION_MERCHANT);ChangeToStandardFaction(m,STANDARD_FACTION_MERCHANT);SetAILevel(m,AI_LEVEL_NORMAL);
    SetLocalFloat(m,"R",r);
    DelayCommand(10.0,Go(m,b,r));
}
void Report(int i)
{
    object m=GetObjectByTag("M"+IntToString(i));string k="away"+IntToString(i);
    WriteTimestampedLogEntry("RAW|"+k+"|r="+FloatToString(GetLocalFloat(m,"R"),0,0)+" first="+FloatToString(GetLocalFloat(m,"FIRST"),0,2)+" stops="+GetLocalString(m,"STOPS")
        +" dist="+FloatToString(GetDistanceBetween(m,GetObjectByTag("B"+IntToString(i))),0,2)+" seq="+GetLocalString(m,"SEQ"));
    PLogF(k+".first",GetLocalFloat(m,"FIRST"),0.15);
    PLogF(k+".dist",GetDistanceBetween(m,GetObjectByTag("B"+IntToString(i))),0.5);
}
void main()
{
    PStart();int i;
    for(i=0;i<6;i++)Pair(i,i<3?10.0:30.0);
    for(i=0;i<6;i++)DelayCommand(30.0,Report(i));
    DelayCommand(31.0,PDone());
}
