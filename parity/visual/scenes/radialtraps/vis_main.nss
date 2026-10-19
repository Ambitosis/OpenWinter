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

void Hover(float t,string point,string name)
{
    DelayCommand(t,WriteTimestampedLogEntry("VISUAL|hover|"+point));
    DelayCommand(t+0.8,VShot(name));
}

void main()
{
    object pc=GetEnteringObject();
    if(!GetIsPC(pc))return;
    DelayCommand(.5,Setup(pc));
    DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(3.0,VShot("a_world"));
    DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|hover|0.735,0.58"));
    DelayCommand(4.3,WriteTimestampedLogEntry("VISUAL|mouse|0.735,0.58|down|right"));
    DelayCommand(5.1,VShot("b_root"));
    DelayCommand(5.3,WriteTimestampedLogEntry("VISUAL|mouse|0.735,0.58|up|right"));
    Hover(5.5,"0.657,0.58","b_unlock_label");
    DelayCommand(6.8,WriteTimestampedLogEntry("VISUAL|hover|0.790,0.678"));
    DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|click|0.790,0.678"));
    DelayCommand(8.0,VShot("c_trap"));
    Hover(9.0,"0.735,0.441","d_north");
    Hover(11.0,"0.813,0.58","e_east");
    Hover(13.0,"0.735,0.719","f_south");
    Hover(15.0,"0.657,0.58","g_west");
    DelayCommand(18.0,VDone());
}
