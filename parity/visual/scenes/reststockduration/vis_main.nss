#include "visual_inc"
#include "rd_inc"
void Buff(object pc,object target)
{
    RDState(target,"before_buff");
    AssignCommand(pc,ActionCastSpellAtObject(SPELL_SHIELD_OF_FAITH,target,METAMAGIC_NONE,TRUE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE));
}
void Shot(object pc,object control,string name){RDState(pc,name);RDState(control,name);VShot(name);}
void Rest(object pc)
{
    object control=GetLocalObject(pc,"control");
    WriteTimestampedLogEntry("VISUAL|note|reststockduration|enemy_pc_control="+IntToString(GetIsEnemy(control,pc))+"|enemy_control_pc="+IntToString(GetIsEnemy(pc,control)));
    RDState(pc,"before_rest");AssignCommand(pc,ActionRest());
}
void Cancel(object pc){RDState(pc,"before_cancel");AssignCommand(pc,ClearAllActions(TRUE));}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"started"))return;
    SetLocalInt(GetModule(),"started",1);SetLocalFloat(GetModule(),"start",RDNow());SetTag(pc,"REST_PC");SetPlotFlag(pc,TRUE);
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    object control=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),GetPosition(pc)+Vector(2.0,0.0,0.0),270.0));
    SetTag(control,"NO_REST");int i;for(i=5000;i<=5013;i++)SetEventScript(control,i,"");
    AssignCommand(control,ClearAllActions(TRUE));SetAILevel(control,AI_LEVEL_NORMAL);ChangeToStandardFaction(control,STANDARD_FACTION_MERCHANT);SetPlotFlag(control,TRUE);
    SetLocalObject(pc,"control",control);
    SetEventScript(pc,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"rd_spell");SetEventScript(control,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"rd_spell");
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_REST,"rd_rest");
    WriteTimestampedLogEntry("VISUAL|note|reststockduration|spell450_impact="+Get2DAString("spells","ImpactScript",450));
    DelayCommand(1.0,VCamera(pc,90.0,7.0,65.0));DelayCommand(2.0,Buff(pc,pc));DelayCommand(3.0,Buff(pc,control));
    DelayCommand(4.5,Shot(pc,control,"a_buffed"));DelayCommand(6.0,Rest(pc));
    DelayCommand(6.3,Shot(pc,control,"b_rest_start"));DelayCommand(10.0,Shot(pc,control,"c_during_rest"));
    DelayCommand(12.0,Cancel(pc));DelayCommand(12.4,Shot(pc,control,"d_cancelled"));DelayCommand(15.0,VDone());
}
