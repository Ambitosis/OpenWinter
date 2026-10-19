#include "visual_inc"
#include "rr_inc"
void Finished(object pc){SetLocalInt(pc,"completed",GetLocalInt(pc,"completed")+1);RRState(pc,"queue_finished");}
void Rest(object pc)
{
    SetLocalFloat(pc,"start",RRNow());RRState(pc,"before_request");
    AssignCommand(pc,ActionRest());AssignCommand(pc,ActionDoCommand(Finished(pc)));
    DelayCommand(.10,RRState(pc,"t010"));DelayCommand(.50,RRState(pc,"t050"));
}
void Shot(object pc,string name){RRState(pc,name);VShot(name);}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"started"))return;
    SetLocalInt(GetModule(),"started",1);SetPlotFlag(pc,TRUE);SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_REST,"rr_rest");
    SetEventScript(pc,EVENT_SCRIPT_CREATURE_ON_RESTED,"rr_rested");
    DelayCommand(1.0,VCamera(pc,90.0,6.0,65.0));DelayCommand(2.0,RRKit(pc,0));
    DelayCommand(3.0,Rest(pc));DelayCommand(3.3,Shot(pc,"a_accepted_start"));
    DelayCommand(7.0,RRState(pc,"during_rest"));DelayCommand(14.5,Shot(pc,"b_finished"));
    DelayCommand(16.0,RRKit(pc,1));DelayCommand(17.0,Rest(pc));DelayCommand(17.3,Shot(pc,"c_hook_cancel"));
    DelayCommand(19.0,RRKit(pc,2));DelayCommand(19.5,SetAreaNoRestFlag(TRUE,GetArea(pc)));
    DelayCommand(20.0,Rest(pc));DelayCommand(20.3,Shot(pc,"d_rejected"));DelayCommand(22.0,VDone());
}
