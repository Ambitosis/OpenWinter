#include "visual_inc"
#include "cl_inc"
void Quiet(object c)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(c,i,"");
    AssignCommand(c,ClearAllActions(TRUE));SetAILevel(c,AI_LEVEL_NORMAL);
}
object Actor(object pc,string tag,float x)
{
    vector pos=GetPosition(pc);location origin=Location(GetArea(pc),Vector(pos.x+x,pos.y+1.0,pos.z),270.0);
    object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",origin);
    SetTag(c,tag);Quiet(c);SetPlotFlag(c,TRUE);ChangeToStandardFaction(c,STANDARD_FACTION_MERCHANT);
    SetCreatureAppearanceType(c,APPEARANCE_TYPE_RAT);
    SetLocalLocation(c,"origin",origin);SetLocalObject(c,"expected",c);
    SetLocalLocation(c,"target",Location(GetArea(pc),Vector(pos.x+x,pos.y-2.0,pos.z),0.0));
    return c;
}
void Finished(object c)
{
    SetLocalInt(c,"finished",GetLocalInt(c,"finished")+1);State(c,"queue_finished");
}
void Cast(object c)
{
    SetLocalFloat(c,"start",CLNow());State(c,"request");
    AssignCommand(c,ActionCastSpellAtLocation(0,GetLocalLocation(c,"target"),METAMAGIC_NONE,TRUE));
    AssignCommand(c,ActionDoCommand(Finished(c)));
}
void Shot(object a,object b,object c,string name)
{
    State(a,name);State(b,name);State(c,name);VShot(name);
}
void Setup(object a,object b,object c)
{
    Quiet(a);Quiet(b);Quiet(c);
    ReplaceObjectAnimation(b,"ccastout","cdead");ReplaceObjectAnimation(c,"ccastoutlp","cdead");
    CLNote("table_conjure="+Get2DAString("spells","ConjTime",0)+"|table_cast="+Get2DAString("spells","CastTime",0)
        +"|animation="+Get2DAString("spells","CastAnim",0)+"|replacement=RELEASE:ccastout->cdead,LOOP:ccastoutlp->cdead");
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"started"))return;
    SetLocalInt(GetModule(),"started",1);VBegin(pc);VHidePC(pc);
    object a=Actor(pc,"STOCK",-1.4);object b=Actor(pc,"RELEASE",0.0);object c=Actor(pc,"LOOP",1.4);
    DelayCommand(.3,Setup(a,b,c));DelayCommand(1.0,VCamera(pc,90.0,6.0,65.0));
    DelayCommand(3.0,Shot(a,b,c,"a_idle"));
    DelayCommand(5.0,Cast(a));DelayCommand(5.0,Cast(b));DelayCommand(5.0,Cast(c));
    DelayCommand(6.1,Shot(a,b,c,"b_release_110"));
    DelayCommand(6.35,Shot(a,b,c,"c_boundary_135"));DelayCommand(6.55,Shot(a,b,c,"d_boundary_155"));
    DelayCommand(6.75,Shot(a,b,c,"e_boundary_175"));DelayCommand(7.4,Shot(a,b,c,"f_recovery_240"));
    DelayCommand(10.6,Shot(a,b,c,"g_finished_560"));DelayCommand(12.5,VDone());
}
