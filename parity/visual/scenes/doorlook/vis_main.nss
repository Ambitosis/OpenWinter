#include "visual_inc"
void main()
{
    object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN); SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    object door=GetObjectByTag("linked");SetLocked(door,FALSE);
    DelayCommand(2.0,VCamera(pc,90.0,12.0,60.0));
    DelayCommand(4.0,VShot("a_closed"));
    DelayCommand(6.0,AssignCommand(door,ActionOpenDoor(door)));
    DelayCommand(9.0,VShot("b_open_unhovered"));
    DelayCommand(11.0,WriteTimestampedLogEntry("VISUAL|hover|0.65,0.32"));
    DelayCommand(12.0,WriteTimestampedLogEntry("VISUAL|rightclick|0.65,0.32"));
    DelayCommand(13.0,WriteTimestampedLogEntry("VISUAL|key|101|tap"));
    DelayCommand(15.0,VShot("c_transition_hover"));
    DelayCommand(17.0,WriteTimestampedLogEntry("VISUAL|hover|0.385,0.32"));
    DelayCommand(18.0,WriteTimestampedLogEntry("VISUAL|rightclick|0.385,0.32"));
    DelayCommand(19.0,WriteTimestampedLogEntry("VISUAL|key|101|tap"));
    DelayCommand(21.0,VShot("d_plain_opening"));
    DelayCommand(23.0,AssignCommand(door,ActionCloseDoor(door)));
    DelayCommand(26.0,VShot("e_closed"));
    DelayCommand(28.0,VDone());
}
