#include "hr_inc"
void Quiet(object o)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE));SetAILevel(o,AI_LEVEL_NORMAL);
}
void Followup(object a,object t,int mode)
{
    HState(a,t,"before_refresh");
    if(mode<2)SignalEvent(t,EventSpellCastAt(a,601,mode==0));
    else AssignCommand(a,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(1,DAMAGE_TYPE_MAGICAL),t));
    DelayCommand(.1,HState(a,t,"after_refresh"));
}
void Start(object a,object t,int mode)
{
    string k=GetTag(a);SetLocalFloat(a,"start",PNow());SetLocalInt(t,"hp",GetCurrentHitPoints(t));
    PLogI(k+".valid",GetIsObjectValid(a)&&GetIsObjectValid(t)&&GetArea(a)==GetArea(t));
    PLogF(k+".distance",GetDistanceBetween(a,t),0.01);PLogI(k+".seen",GetObjectSeen(t,a));
    int i;for(i=0;i<3;i++)
    {
        object other=GetLocalObject(GetModule(),"a"+IntToString(i));
        if(other!=a)PLogI(k+".sees_other"+IntToString(i),GetObjectSeen(other,a));
    }
    HState(a,t,"before");SignalEvent(t,EventSpellCastAt(a,601,TRUE));
    DelayCommand(.1,HState(a,t,"initial_active"));DelayCommand(4.0,Followup(a,t,mode));
    DelayCommand(7.5,HState(a,t,"t750"));DelayCommand(8.5,HState(a,t,"t850"));
    DelayCommand(11.5,HState(a,t,"t1150"));DelayCommand(12.1,HState(a,t,"t1210"));
}
void Setup(object a,object t)
{
    Quiet(a);Quiet(t);SetPlotFlag(a,FALSE);SetPlotFlag(t,FALSE);
    ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE);ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);
    SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"hr_spell");SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_DAMAGED,"hr_damage");
}
void Pair(string id,float x,float y,int mode)
{
    location ca=Location(GetObjectByTag("PARITY_AREA"),Vector(x,y,0.0),90.0);
    location ta=Location(GetObjectByTag("PARITY_AREA"),Vector(x,y+6.0,0.0),270.0);
    object a=CreateObject(OBJECT_TYPE_CREATURE,"sa_target",ca);object t=CreateObject(OBJECT_TYPE_CREATURE,"sa_target",ta);
    SetTag(a,id);SetLocalObject(t,"caster",a);SetLocalLocation(a,"origin",ca);SetLocalLocation(t,"origin",ta);
    SetLocalObject(GetModule(),"a"+IntToString(mode),a);
    DelayCommand(.3,Setup(a,t));DelayCommand(5.0,Start(a,t,mode));
}
void main()
{
    Pair("hostile",20.0,20.0,0);Pair("friendly",80.0,20.0,1);Pair("damage",20.0,80.0,2);
    DelayCommand(19.0,PDone());
}
