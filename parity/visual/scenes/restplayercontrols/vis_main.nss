#include "visual_inc"
#include "rc_inc"
void Finished(object pc){SetLocalInt(pc,"completed",GetLocalInt(pc,"completed")+1);RCState(pc,"queue_finished");}
void Rest(object pc,int trial)
{
    SetLocalInt(pc,"trial",trial);SetLocalFloat(pc,"start",RCNow());
    ReplaceObjectAnimation(pc,"sitcross","sitcross");ReplaceObjectAnimation(pc,"sitcrossps","sitcrossps");
    if(trial==1)ReplaceObjectAnimation(pc,"sitcross","salute");
    if(trial==2)ReplaceObjectAnimation(pc,"sitcrossps","deadfnt");
    RCState(pc,"before_request");AssignCommand(pc,ActionRest());AssignCommand(pc,ActionDoCommand(Finished(pc)));
}
void Shot(object pc,string name){RCState(pc,name);VShot(name);}
void Buff(object pc){AssignCommand(pc,ActionCastSpellAtObject(SPELL_SHIELD_OF_FAITH,pc,METAMAGIC_NONE,TRUE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE));}
void Mouse(string state){WriteTimestampedLogEntry("VISUAL|mouse|0.543,0.444|"+state);}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"started"))return;
    SetLocalInt(GetModule(),"started",1);SetPlotFlag(pc,TRUE);SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc,1.0,89.0,1.0,40.0);SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_REST,"rc_rest");
    DelayCommand(1.0,VCamera(pc,90.0,6.0,65.0));DelayCommand(2.0,Buff(pc));
    DelayCommand(5.0,Rest(pc,1));DelayCommand(5.25,Shot(pc,"a_intro_replaced"));
    DelayCommand(7.5,Shot(pc,"b_intro_replaced_loop"));DelayCommand(15.65,Shot(pc,"c_intro_replaced_exit"));
    DelayCommand(16.4,Shot(pc,"d_intro_replaced_exit"));DelayCommand(18.0,Shot(pc,"e_finished"));
    DelayCommand(20.0,Rest(pc,2));DelayCommand(20.3,Shot(pc,"f_loop_replaced_intro"));
    DelayCommand(22.5,Shot(pc,"g_loop_replaced"));DelayCommand(30.7,Shot(pc,"h_loop_replaced_exit"));
    DelayCommand(32.5,Shot(pc,"i_finished"));DelayCommand(34.0,Buff(pc));
    DelayCommand(37.0,Rest(pc,3));DelayCommand(37.4,Shot(pc,"j_hook_cancel"));
    DelayCommand(40.0,Rest(pc,4));DelayCommand(42.5,Shot(pc,"k_before_cancel_click"));
    DelayCommand(43.0,WriteTimestampedLogEntry("VISUAL|hover|0.543,0.444"));
    DelayCommand(43.5,Mouse("down"));DelayCommand(43.6,Mouse("up"));
    DelayCommand(44.0,Shot(pc,"l_after_cancel_click"));DelayCommand(46.0,Shot(pc,"m_cancel_settled"));
    DelayCommand(48.0,VDone());
}
