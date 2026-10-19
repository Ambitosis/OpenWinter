#include "visual_inc"
float Now() { return IntToFloat(GetTimeSecond()) + IntToFloat(GetTimeMillisecond()) / 1000.0; }
void Trial(object pc, object door, int n, int self)
{
    object actor = pc; if (self) actor = door;
    int open = GetIsOpen(door);
    WriteTimestampedLogEntry("VISUAL|note|attempt|self=" + IntToString(self) + "|n=" + IntToString(n)
        + "|t=" + FloatToString(Now(),0,3) + "|open=" + IntToString(open));
    AssignCommand(actor, ClearAllActions(TRUE));
    if (open) AssignCommand(actor, ActionCloseDoor(door));
    else AssignCommand(actor, ActionOpenDoor(door));
    if (n < 29) DelayCommand(0.1, Trial(pc,door,n+1,self));
}
void main()
{
    object pc=GetEnteringObject(); if (!GetIsPC(pc)) return;
    object door=GetObjectByTag("audio_door"); SetLocked(door,FALSE); SetPlotFlag(door,TRUE);
    SetEventScript(door,EVENT_SCRIPT_DOOR_ON_OPEN,"dr_open"); SetEventScript(door,EVENT_SCRIPT_DOOR_ON_CLOSE,"dr_close");
    DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(3.0,VShot("initial_closed"));
    DelayCommand(4.0,Trial(pc,door,0,FALSE));
    DelayCommand(9.0,VShot("a_after_pc"));
    DelayCommand(12.0,Trial(pc,door,0,TRUE));
    DelayCommand(17.0,VShot("b_after_self"));
    DelayCommand(19.0,AssignCommand(pc,ActionOpenDoor(door)));
    DelayCommand(19.0,AssignCommand(pc,ActionCloseDoor(door)));
    DelayCommand(19.0,AssignCommand(pc,ActionOpenDoor(door)));
    DelayCommand(19.0,AssignCommand(pc,ActionCloseDoor(door)));
    DelayCommand(27.0,VShot("queued_actions"));
    DelayCommand(29.0,VDone());
}
