#include "visual_inc"

void Setup(object pc)
{
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
    WriteTimestampedLogEntry("VISUAL|note|skills|disable="+IntToString(GetSkillRank(SKILL_DISABLE_TRAP,pc,TRUE))
        +"|lock="+IntToString(GetSkillRank(SKILL_OPEN_LOCK,pc,TRUE)));
    object o=VPlaceable(pc,"plc_chest1",3.0,0.0);
    SetTag(o,"RT_CHEST");
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE,o);
    SetTrapDetectedBy(o,pc,TRUE);
    SetLocked(o,TRUE);
    WriteTimestampedLogEntry("VISUAL|note|trap|present="+IntToString(GetIsTrapped(o))
        +"|detected="+IntToString(GetTrapDetectedBy(o,pc))
        +"|disarm="+IntToString(GetTrapDisarmable(o))+"|recover="+IntToString(GetTrapRecoverable(o)));
}

void main()
{
    object pc=GetEnteringObject();
    if(!GetIsPC(pc))return;
    DelayCommand(.5,Setup(pc));
    DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(20.0,VDone());
}
