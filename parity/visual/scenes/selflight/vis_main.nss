#include "visual_inc"
void Key(string state) { WriteTimestampedLogEntry("VISUAL|key|9|"+state); }
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(1.0,VCamera(p,90.0,5.0,65.0));
    DelayCommand(3.0,VShot("a_plain"));
    DelayCommand(4.0,Key("down"));
    DelayCommand(6.0,VShot("b_front"));
    DelayCommand(7.0,AssignCommand(p,SetFacing(90.0)));
    DelayCommand(9.0,VShot("c_back"));
    DelayCommand(10.0,Key("up"));
    DelayCommand(12.0,VShot("d_released"));
    DelayCommand(13.0,VDone());
}
