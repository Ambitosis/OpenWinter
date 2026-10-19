#include "visual_inc"
void Mark(object pc,int detected,string phase)
{
    object trap=GetLocalObject(pc,"trap");
    SetTrapDetectedBy(trap,pc,detected);
    WriteTimestampedLogEntry("VISUAL|note|"+phase+"|valid="+IntToString(GetIsObjectValid(trap))+"|detected="+IntToString(GetTrapDetectedBy(trap,pc)));
}
void Setup(object pc)
{
    object trap=VPlaceable(pc,"plc_chest1",3.0,0.0);
    SetTag(trap,"TINT_TRAP");SetLocalObject(pc,"trap",trap);
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE,trap);
    SetTrapDetectable(trap,FALSE); // exclude passive rediscovery after explicit clears
    Mark(pc,TRUE,"initial");
}
void Shot(object pc,string name)
{
    object trap=GetLocalObject(pc,"trap");
    WriteTimestampedLogEntry("VISUAL|note|shot_"+name+"|valid="+IntToString(GetIsObjectValid(trap))+"|detected="+IntToString(GetTrapDetectedBy(trap,pc))+"|detectable="+IntToString(GetTrapDetectable(trap)));
    VShot(name);
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    DelayCommand(.5,Setup(pc));
    DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(2.2,WriteTimestampedLogEntry("VISUAL|hover|0.1,0.1"));
    DelayCommand(3.0,Shot(pc,"early_detected"));
    DelayCommand(5.0,Mark(pc,FALSE,"clear"));
    DelayCommand(6.0,Shot(pc,"cleared"));
    DelayCommand(7.0,Mark(pc,TRUE,"late"));
    DelayCommand(8.0,Shot(pc,"late_detected"));
    DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|hover|0.735,0.58"));
    DelayCommand(10.0,Shot(pc,"hovered"));
    DelayCommand(11.0,Mark(pc,FALSE,"final_clear"));
    DelayCommand(11.2,WriteTimestampedLogEntry("VISUAL|hover|0.1,0.1"));
    DelayCommand(12.0,Shot(pc,"final_cleared"));
    DelayCommand(13.0,VDone());
}
