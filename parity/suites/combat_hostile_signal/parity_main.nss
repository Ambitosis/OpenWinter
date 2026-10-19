#include "hs_inc"
void Quiet(object o)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE));SetAILevel(o,AI_LEVEL_NORMAL);
}
void ExpiryPoll(object a,object t)
{
    string k=GetTag(a);float now=PNow();float elapsed=now-GetLocalFloat(a,"start");
    int count=GetLocalInt(a,"poll_count");SetLocalInt(a,"poll_count",count+1);
    if(count==0)
    {
        PLogI(k+".poll_active_gate",GetIsInCombat(a)&&GetIsInCombat(t));
        PLogF(k+".poll_start_absolute",now,0.10);
    }
    if(GetIsInCombat(a))SetLocalFloat(a,"a_last_active",now);
    else if(!GetLocalInt(a,"a_expired"))
    {
        SetLocalInt(a,"a_expired",1);
        PLogF(k+".a_expiry_relative",elapsed,0.05);PLogF(k+".a_expiry_absolute",now,0.05);
        PLogF(k+".a_last_active_absolute",GetLocalFloat(a,"a_last_active"),0.05);
    }
    if(GetIsInCombat(t))SetLocalFloat(a,"t_last_active",now);
    else if(!GetLocalInt(a,"t_expired"))
    {
        SetLocalInt(a,"t_expired",1);
        PLogF(k+".t_expiry_relative",elapsed,0.05);PLogF(k+".t_expiry_absolute",now,0.05);
        PLogF(k+".t_last_active_absolute",GetLocalFloat(a,"t_last_active"),0.05);
    }
    if(GetLocalInt(a,"a_expired")&&GetLocalInt(a,"t_expired"))PLogI(k+".poll_completed",1);
    else if(elapsed<10.0)DelayCommand(.025,ExpiryPoll(a,t));
    else PLogI(k+".poll_completed",0);
}
void Start(object a,object t,int mode)
{
    string k=GetTag(a);SetLocalFloat(a,"start",PNow());SetLocalInt(t,"hp",GetCurrentHitPoints(t));
    PLogF(k+".trigger_absolute",PNow(),0.05);
    if(mode==0||mode==2)DelayCommand(7.0,ExpiryPoll(a,t));
    PLogI(k+".valid",GetIsObjectValid(a)&&GetIsObjectValid(t)&&GetArea(a)==GetArea(t));
    PLogI(k+".position",GetDistanceBetweenLocations(GetLocation(a),GetLocalLocation(a,"origin"))<0.001&&GetDistanceBetweenLocations(GetLocation(t),GetLocalLocation(t,"origin"))<0.001);
    PLogF(k+".distance",GetDistanceBetween(a,t),0.01);PLogI(k+".seen",GetObjectSeen(t,a));
    int i;for(i=0;i<4;i++)
    {
        object other=GetLocalObject(GetModule(),"a"+IntToString(i));
        if(other!=a)PLogI(k+".sees_other"+IntToString(i),GetObjectSeen(other,a));
    }
    HState(a,t,"before");
    if(mode==0)SignalEvent(t,EventSpellCastAt(a,601,TRUE));
    else if(mode==1)SignalEvent(t,EventSpellCastAt(a,601,FALSE));
    else if(mode==2)AssignCommand(a,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(1,DAMAGE_TYPE_MAGICAL),t));
    HState(a,t,"after_call");
    DelayCommand(.02,HState(a,t,"t002"));DelayCommand(.1,HState(a,t,"t010"));
    DelayCommand(.05,HState(a,t,"t005"));DelayCommand(.5,HState(a,t,"t050"));
    DelayCommand(6.0,HState(a,t,"t600"));DelayCommand(7.0,HState(a,t,"t700"));
    DelayCommand(7.5,HState(a,t,"t750"));DelayCommand(8.0,HState(a,t,"t800"));
    DelayCommand(8.25,HState(a,t,"t825"));DelayCommand(8.5,HState(a,t,"t850"));
    DelayCommand(9.0,HState(a,t,"t900"));DelayCommand(10.0,HState(a,t,"t1000"));
    DelayCommand(12.0,HState(a,t,"t1200"));DelayCommand(14.0,HState(a,t,"t1400"));
}
void Setup(object a,object t)
{
    Quiet(a);Quiet(t);SetPlotFlag(t,FALSE);
    ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE);ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);
    SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"hs_spell");SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_DAMAGED,"hs_damage");
}
void Final(object a,object t)
{
    HState(a,t,"final");PLogI(GetTag(a)+".stationary",GetDistanceBetweenLocations(GetLocation(a),GetLocalLocation(a,"origin"))<0.001&&GetDistanceBetweenLocations(GetLocation(t),GetLocalLocation(t,"origin"))<0.001);
}
void Pair(string id,float x,float y,int mode)
{
    location ca=Location(GetObjectByTag("PARITY_AREA"),Vector(x,y,0.0),90.0);
    location ta=Location(GetObjectByTag("PARITY_AREA"),Vector(x,y+6.0,0.0),270.0);
    object a=CreateObject(OBJECT_TYPE_CREATURE,"sa_target",ca);object t=CreateObject(OBJECT_TYPE_CREATURE,"sa_target",ta);
    SetTag(a,id);SetLocalObject(t,"caster",a);SetLocalLocation(a,"origin",ca);SetLocalLocation(t,"origin",ta);
    SetLocalObject(GetModule(),"a"+IntToString(mode),a);
    DelayCommand(.3,Setup(a,t));DelayCommand(mode==2?5.37:5.0,Start(a,t,mode));DelayCommand(20.0,Final(a,t));
}
void main()
{
    Pair("hostile",20.0,20.0,0);Pair("friendly",80.0,20.0,1);
    Pair("damage",20.0,80.0,2);Pair("noop",80.0,80.0,3);
    DelayCommand(21.0,PDone());
}
