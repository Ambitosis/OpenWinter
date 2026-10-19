#include "visual_inc"
#include "rp_inc"
void Quiet(object c)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(c,i,"");
    AssignCommand(c,ClearAllActions(TRUE));SetAILevel(c,AI_LEVEL_NORMAL);
    ChangeToStandardFaction(c,STANDARD_FACTION_MERCHANT);SetPlotFlag(c,TRUE);
}
object Actor(object pc,string tag,float x)
{
    vector p=GetPosition(pc);
    object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(p.x+x,p.y,p.z),270.0));
    SetTag(c,tag);Quiet(c);return c;
}
void Finished(object c){SetLocalInt(c,"completed",GetLocalInt(c,"completed")+1);RPState(c,"queue_finished");}
void Rest(object c)
{
    SetLocalFloat(c,"start",RPNow());RPState(c,"before_request");
    AssignCommand(c,ActionRest());AssignCommand(c,ActionDoCommand(Finished(c)));
}
void Shot(object a,object b,object c,string name){RPState(a,name);RPState(b,name);RPState(c,name);VShot(name);}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"started"))return;
    SetLocalInt(GetModule(),"started",1);SetTag(pc,"PC_STOCK");SetPlotFlag(pc,TRUE);
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_REST,"rp_rest");
    object intro=Actor(pc,"INTRO",-1.7),loop=Actor(pc,"LOOP",1.7);
    ReplaceObjectAnimation(intro,"sitcross","salute");ReplaceObjectAnimation(loop,"sitcrossps","deadfnt");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectACIncrease(2),pc,90.0);
    DelayCommand(1.0,VCamera(pc,90.0,8.0,65.0));
    DelayCommand(3.0,Shot(pc,intro,loop,"a_before"));
    DelayCommand(5.0,Rest(pc));DelayCommand(5.0,Rest(intro));DelayCommand(5.0,Rest(loop));
    DelayCommand(5.25,Shot(pc,intro,loop,"b_entry"));
    DelayCommand(6.0,Shot(pc,intro,loop,"c_entry"));
    DelayCommand(7.2,Shot(pc,intro,loop,"d_loop"));
    DelayCommand(10.0,Shot(pc,intro,loop,"e_loop"));
    DelayCommand(15.7,Shot(pc,intro,loop,"f_late"));
    DelayCommand(16.6,Shot(pc,intro,loop,"g_exit"));
    DelayCommand(17.4,Shot(pc,intro,loop,"h_exit"));
    DelayCommand(19.0,Shot(pc,intro,loop,"i_finished"));
    DelayCommand(22.0,VDone());
}
