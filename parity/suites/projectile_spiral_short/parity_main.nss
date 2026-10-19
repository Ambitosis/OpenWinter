#include "parity_inc"
#include "parity_time"
void Quiet(object o)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(o,i,"");
    SetAILevel(o,AI_LEVEL_NORMAL);SetPlotFlag(o,TRUE);
}
void Finish(object c)
{
    PLogF(GetLocalString(c,"case")+".finish",PNow()-GetLocalFloat(c,"start"),0.10);
}
void Cast(int id,int spell)
{
    object c=GetLocalObject(GetModule(),"c"+IntToString(id));
    string k=(spell==0?"control":"spiral")+".d"+IntToString(id);
    SetLocalString(c,"case",k);SetLocalInt(c,"impacts",0);
    location origin=GetLocalLocation(c,"origin");location target=GetLocalLocation(c,"target");
    vector at=GetPosition(c);vector dest=GetPositionFromLocation(target);
    PLogI(k+".valid",GetIsObjectValid(c));
    PLogI(k+".position",GetDistanceBetweenLocations(GetLocation(c),origin)<0.001);
    PLogF(k+".distance",GetDistanceBetweenLocations(GetLocation(c),target),0.001);
    PLogF(k+".cx",at.x,0.001);PLogF(k+".cy",at.y,0.001);
    PLogF(k+".tx",dest.x,0.001);PLogF(k+".ty",dest.y,0.001);
    SetLocalFloat(c,"start",PNow());
    AssignCommand(c,ActionCastSpellAtLocation(spell,target,METAMAGIC_NONE,TRUE,PROJECTILE_PATH_TYPE_DEFAULT));
    AssignCommand(c,ActionDoCommand(Finish(c)));
}
void Verify(int spell)
{
    int i;for(i=0;i<6;i++)
    {
        object c=GetLocalObject(GetModule(),"c"+IntToString(i));
        string k=(spell==0?"control":"spiral")+".d"+IntToString(i);
        PLogI(k+".executed",GetLocalInt(c,"impacts"));
    }
}
void Wave(int spell)
{
    int i;for(i=0;i<6;i++)DelayCommand(IntToFloat(i)*.03,Cast(i,spell));
    DelayCommand(7.0,Verify(spell));
}
void Make(int id,float distance)
{
    object area=GetObjectByTag("PARITY_AREA");float y=8.0+IntToFloat(id)*12.0;
    location origin=Location(area,Vector(12.0,y,0.0),0.0);
    object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",origin);Quiet(c);
    SetLocalLocation(c,"origin",origin);SetLocalLocation(c,"target",Location(area,Vector(12.0+distance,y,0.0),0.0));
    SetLocalObject(GetModule(),"c"+IntToString(id),c);
}
void main()
{
    Make(0,0.0);Make(1,0.25);Make(2,0.5);Make(3,0.75);Make(4,1.0);Make(5,1.5);
    DelayCommand(3.0,Wave(0));DelayCommand(11.0,Wave(1));DelayCommand(20.0,PDone());
}
