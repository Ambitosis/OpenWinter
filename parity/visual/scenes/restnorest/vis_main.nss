// Resting in an area with No Rest set (docs/DEATH_AND_DYING.md): the refusal line, and whether OnPlayerRest runs.
#include "dy_inc"
#include "visual_inc"
void Rest(object pc){DyNote(pc,"rest_request");AssignCommand(pc,ActionRest());DelayCommand(0.3,DyNote(pc,"rest_0.3"));}
void Done(){VDone();}
void main()
{
    object pc=GetEnteringObject();object m=GetModule();
    if(!GetIsPC(pc)||GetLocalInt(m,"started"))return;
    SetLocalInt(m,"started",1);SetLocalFloat(m,"t0",DyNow());
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_REST,"dy_rest");
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    DelayCommand(0.5,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(2.0,Rest(pc));
    DelayCommand(3.0,VShot("a_norest"));
    DelayCommand(5.0,Done());
}
