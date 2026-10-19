// ActionMoveToObject per target type. Walkers are stripped bandits (merchant).
//   TG_C01/C10/C50 and TG_N01/N10/N50: an ordinary 6 x 6 m trigger (position at its south-west corner) with a logging
//     OnClick (C) or none (N), walker 6 m south-west of the corner, fRange 0.1, 1.0 and 5.0: where it stops (flat
//     distance to the outline), whether its action has ended, and whether OnClick fired.
//   TG_IN / TG_INN: walker inside a 12 x 12 trigger (with and without OnClick), 6 m from every edge.
//   TP_*: a trap trigger (CreateTrapAtLocation, 2 x 2 m... size 2.0) of the walker's own faction (it can't go off on it),
//     walker 6 m south-west: where it stops, how often it stops and sets off again (distance sampled every 0.1 s).
//   TR_PLAIN / TR_CLICK: area-transition triggers to waypoints in AC_B, with no OnClick and with a logging OnClick.
//   DR_SHUT / DR_OPEN: transition doors (closed, and opened beforehand) to waypoints in AC_B.
#include "parity_inc"
#include "parity_time"
object A(){return GetObjectByTag("AC_A");}
location L(float x,float y){return Location(A(),Vector(x,y,0.0),45.0);}
object W(string k,float x,float y)
{
    object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",L(x,y),FALSE,"W_"+k);
    int e;for(e=5000;e<=5012;e++)SetEventScript(o,e,"");ChangeToStandardFaction(o,STANDARD_FACTION_MERCHANT);SetAILevel(o,AI_LEVEL_NORMAL);SetPlotFlag(o,TRUE);
    SetLocalString(GetModule(),"CASES",GetLocalString(GetModule(),"CASES")+k+" ");
    return o;
}
// Flat distance from p to the outline of the rectangle [x0,x1] x [y0,y1].
float Edge(vector p,float x0,float y0,float x1,float y1)
{
    float dx=0.0,dy=0.0;
    if(p.x>=x0&&p.x<=x1&&p.y>=y0&&p.y<=y1){float m=p.x-x0;if(x1-p.x<m)m=x1-p.x;if(p.y-y0<m)m=p.y-y0;if(y1-p.y<m)m=y1-p.y;return -m;}
    if(p.x<x0)dx=x0-p.x;else if(p.x>x1)dx=p.x-x1;
    if(p.y<y0)dy=y0-p.y;else if(p.y>y1)dy=p.y-y1;
    return sqrt(dx*dx+dy*dy);
}
void Track(object o,vector prev,int n)
{
    vector p=GetPosition(o);int moving=VectorMagnitude(p-prev)>0.01;
    if(moving&&!GetLocalInt(o,"MOV"))SetLocalInt(o,"STARTS",GetLocalInt(o,"STARTS")+1);
    SetLocalInt(o,"MOV",moving);
    if(n>0)DelayCommand(0.1,Track(o,p,n-1));
}
void Setup()
{
    string g="TG_C01 TG_C10 TG_C50 TG_N01 TG_N10 TG_N50 ";int j=0;while(g!=""){int q=FindSubString(g," ");W(GetStringLeft(g,q),10.0+12.0*IntToFloat(j)-4.24,10.0-4.24);g=GetSubString(g,q+1,1000);j++;}
    W("TG_IN",16.0,36.0);W("TG_INN",36.0,36.0);
    int i;
    for(i=0;i<3;i++)
    {
        float x=30.0+IntToFloat(i)*12.0;
        object t=CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE,L(x,70.0),2.0,"TP_"+IntToString(i),STANDARD_FACTION_MERCHANT,"","");
        SetEventScript(t,EVENT_SCRIPT_TRIGGER_ON_CLICKED,"ac_click");
        W("TP_"+IntToString(i),x-6.0,64.0);
    }
    W("TR_PLAIN",60.0,26.0);W("TR_CLICK",75.0,26.0);W("DR_SHUT",40.0,49.0);W("DR_OPEN",55.0,49.0);
    AssignCommand(GetObjectByTag("DR_OPEN"),ActionOpenDoor(GetObjectByTag("DR_OPEN")));
}
void Go()
{
    string s=GetLocalString(GetModule(),"CASES");
    while(s!=""){int p=FindSubString(s," ");string k=GetStringLeft(s,p);s=GetSubString(s,p+1,10000);
        object o=GetObjectByTag("W_"+k);float r=GetStringRight(k,2)=="01"?0.1:(GetStringRight(k,2)=="50"?5.0:1.0);
        object t=GetObjectByTag(k);SetLocalObject(o,"T",t);
        AssignCommand(o,ActionMoveToObject(t,FALSE,r));SetLocalFloat(o,"T0",PSince());Track(o,GetPosition(o),180);}
}
void ReadOne(string k)
{
    object o=GetObjectByTag("W_"+k),t=GetLocalObject(o,"T");vector p=GetPosition(o);vector q=GetPosition(t);
    float e=-99.0;
    if(GetStringLeft(k,2)=="TG"){float w=GetStringLeft(k,5)=="TG_IN"?12.0:6.0;e=Edge(p,q.x,q.y,q.x+w,q.y+w);}
    if(GetStringLeft(k,2)=="TP"){e=Edge(p,q.x-1.0,q.y-1.0,q.x+1.0,q.y+1.0);}
    WriteTimestampedLogEntry("RAW|"+k+"|area="+GetTag(GetArea(o))+" pos="+FloatToString(p.x,0,2)+","+FloatToString(p.y,0,2)+" tpos="+FloatToString(q.x,0,2)+","+FloatToString(q.y,0,2)
        +" edge="+FloatToString(e,0,2)+" click="+GetLocalString(t,"CLICK")+" entered="+IntToString(GetLocalInt(o,"ENTERED_"+k))+" starts="+IntToString(GetLocalInt(o,"STARTS"))
        +" act="+IntToString(GetCurrentAction(o))+" open="+IntToString(GetIsOpen(t)));
    PLog(k+".area",GetTag(GetArea(o)));
    if(e>-99.0)PLogF(k+".edge",e,0.1);
    PLogI(k+".clicked",GetLocalInt(o,"CLICKED_"+k));
    PLogI(k+".entered",GetLocalInt(o,"ENTERED_"+k));
    PLog(k+".starts",IntToString(GetLocalInt(o,"STARTS"))+"|~1");
    if(GetStringLeft(k,2)=="TP"||GetStringLeft(k,4)=="TG_C")PLog(k+".held20",IntToString(GetLocalInt(o,"ACT20"))+"|~1");else PLogI(k+".held20",GetLocalInt(o,"ACT20"));
}
void Act(string when)
{
    string s=GetLocalString(GetModule(),"CASES");
    while(s!=""){int p=FindSubString(s," ");string k=GetStringLeft(s,p);s=GetSubString(s,p+1,10000);
        object o=GetObjectByTag("W_"+k);
        int held=GetCurrentAction(o)!=ACTION_INVALID;
        // A walker held at a trap trigger or outside a clickable trigger is now and then between attempts with no
        // action at a reading (nwserver: 2 of 3 trap walkers at 20 s and none at 45 s; 2 of 3 clickable-trigger walkers
        // in one run of three): either is accepted there.
        if(when=="20")SetLocalInt(o,"ACT20",held);
        else if(GetStringLeft(k,2)=="TP"||GetStringLeft(k,4)=="TG_C")PLog(k+".held"+when,IntToString(held)+"|~1");
        else PLogI(k+".held"+when,held);}
}
void Read()
{
    string s=GetLocalString(GetModule(),"CASES");
    while(s!=""){int p=FindSubString(s," ");ReadOne(GetStringLeft(s,p));s=GetSubString(s,p+1,10000);}
}
void main()
{
    PStart();
    DelayCommand(1.0,Setup());
    DelayCommand(8.0,Go());
    // The move action's state 20 s after the order, and 45 s after (whether one that never reaches gives up).
    DelayCommand(28.0,Act("20"));
    DelayCommand(40.0,Read());
    DelayCommand(53.0,Act("45"));
    DelayCommand(54.0,PDone());
}
