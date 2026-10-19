// Same door control, with an actor-filtered public PC OnPerception event handler.
// Door motion is outside the model's intended image region; reject if it occludes it.
#include "visibility_inc"

float CVNow()
{
    int minutes = FloatToInt(HoursToSeconds(1)) / 60;
    return IntToFloat((GetTimeHour()*minutes+GetTimeMinute())*60+GetTimeSecond())
        + IntToFloat(GetTimeMillisecond())/1000.0;
}

void Sample(object pc, object actor, object door, string stage, int n)
{
    WriteTimestampedLogEntry("VISUAL|note|"+stage+"|sample="+IntToString(n)
        +"|t="+FloatToString(CVNow()-GetLocalFloat(pc,"cv_start"),0,3)
        +"|open="+IntToString(GetIsOpen(door))
        +"|seen="+IntToString(GetObjectSeen(actor,pc))
        +"|heard="+IntToString(GetObjectHeard(actor,pc))
        +"|los="+IntToString(LineOfSightObject(pc,actor)));
    if(n<60) DelayCommand(0.1,Sample(pc,actor,door,stage,n+1));
}

void DoorChange(object pc, object actor, object door, int open, string stage)
{
    SetLocalFloat(pc,"cv_start",CVNow());
    SetLocalString(pc,"cv_stage",stage);
    if(open) AssignCommand(door,ActionOpenDoor(door));
    else AssignCommand(door,ActionCloseDoor(door));
    Sample(pc,actor,door,stage,0);
}

void Shot(object pc, object actor, object door, string name, int expectedOpen)
{
    vector a=GetPosition(actor);
    int stable=VectorMagnitude(a-Vector(20.0,23.0,0.0))<0.05;
    WriteTimestampedLogEntry("VISUAL|note|"+name+"|door_valid="
        +IntToString(GetIsObjectValid(door)&&GetIsOpen(door)==expectedOpen)
        +"|actor_position_valid="+IntToString(stable));
    CVReport(pc,actor,name,Vector(20.0,17.0,0.0));
}

void RestoreHandler(object pc)
{
    SetEventScript(pc,EVENT_SCRIPT_CREATURE_ON_NOTICE,GetLocalString(pc,"cv_original"));
}

void main()
{
    object pc=GetEnteringObject();
    if(!GetIsPC(pc))return;
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    SetPlotFlag(pc,TRUE);
    object actor=VSpawn(pc,"cv_actor",0.0,6.0,270.0);
    SetTag(actor,"CV_ACTOR");
    CVQuiet(actor);
    SetLocalFloat(pc,"cv_start",CVNow());
    SetLocalString(pc,"cv_stage","initial");
    string original=GetEventScript(pc,EVENT_SCRIPT_CREATURE_ON_NOTICE);
    SetLocalString(pc,"cv_original",original);
    WriteTimestampedLogEntry("VISUAL|note|pc_handler|original="+original);
    SetEventScript(pc,EVENT_SCRIPT_CREATURE_ON_NOTICE,"cv_seen");
    WriteTimestampedLogEntry("VISUAL|note|pc_handler|installed="
        +GetEventScript(pc,EVENT_SCRIPT_CREATURE_ON_NOTICE));
    SetLocalInt(pc,"cv_control",1);
    ExecuteScript("cv_seen",pc);
    SetLocalInt(pc,"cv_control",0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectCutsceneParalyze(),actor);
    object door=GetObjectByTag("CV_DOOR");
    SetLocked(door,FALSE);
    SetPlotFlag(door,TRUE);
    DelayCommand(2.0,VCamera(pc,90.0,16.0,30.0));
    DelayCommand(6.0,Shot(pc,actor,door,"a_closed",FALSE));
    DelayCommand(8.0,DoorChange(pc,actor,door,TRUE,"opening"));
    DelayCommand(15.0,Shot(pc,actor,door,"b_open",TRUE));
    DelayCommand(17.0,DoorChange(pc,actor,door,FALSE,"closing"));
    DelayCommand(24.0,Shot(pc,actor,door,"c_closed_repeat",FALSE));
    DelayCommand(26.0,DestroyObject(actor));
    DelayCommand(28.0,VShot("d_background_closed"));
    DelayCommand(30.0,AssignCommand(door,ActionOpenDoor(door)));
    DelayCommand(33.0,VShot("e_background_open"));
    DelayCommand(34.0,RestoreHandler(pc));
    DelayCommand(35.0,VDone());
}
