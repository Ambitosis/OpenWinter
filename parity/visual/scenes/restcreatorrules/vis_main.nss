#include "visual_inc"
#include "rc_inc"
void Shot(object pc,string name){RCBoth(pc,name);VShot(name);}
void Rest(object pc){RCBoth(pc,"before_rest");AssignCommand(pc,ActionRest());}
void Cancel(object pc){AssignCommand(pc,ClearAllActions(TRUE));}
void Veto(object pc){SetLocalInt(pc,"veto",1);Rest(pc);}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"started"))return;
    SetLocalInt(GetModule(),"started",1);SetLocalFloat(GetModule(),"start",RCNow());SetTag(pc,"REST_PC");SetPlotFlag(pc,TRUE);
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    object control=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),GetPosition(pc)+Vector(2.0,0.0,0.0),270.0));
    SetTag(control,"NO_REST");int i;for(i=5000;i<=5013;i++)SetEventScript(control,i,"");
    AssignCommand(control,ClearAllActions(TRUE));ChangeToStandardFaction(control,STANDARD_FACTION_MERCHANT);SetPlotFlag(control,TRUE);
    SetLocalObject(pc,"control",control);SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_REST,"rc_rest");
    WriteTimestampedLogEntry("VISUAL|note|restcreatorrules|module="+ObjectToString(GetModule())+"|pc="+ObjectToString(pc)+"|control="+ObjectToString(control)
        +"|enemy_pc_control="+IntToString(GetIsEnemy(control,pc))+"|enemy_control_pc="+IntToString(GetIsEnemy(pc,control)));
    DelayCommand(1.0,VCamera(pc,90.0,7.0,65.0));DelayCommand(2.0,RCSetup(pc));DelayCommand(3.0,Shot(pc,"a_before"));
    DelayCommand(4.0,Rest(pc));DelayCommand(4.3,Shot(pc,"b_started"));DelayCommand(6.0,Cancel(pc));
    DelayCommand(6.3,RCBoth(pc,"after_cancel"));DelayCommand(8.0,RCSetup(pc));DelayCommand(9.0,Veto(pc));
    DelayCommand(9.3,Shot(pc,"c_veto"));DelayCommand(11.0,VDone());
}
