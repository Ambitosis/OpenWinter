#include "visual_inc"
void Hover(string xy) { WriteTimestampedLogEntry("VISUAL|hover|"+xy); }
void Key(string state) { WriteTimestampedLogEntry("VISUAL|key|9|"+state); }
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(1.0,VCamera(p,90.0,7.0,60.0));
    DelayCommand(2.0,Hover("0.90,0.80"));
    DelayCommand(3.0,VShot("a_plain"));
    DelayCommand(4.0,Hover("0.50,0.50"));
    DelayCommand(6.0,VShot("b_hover"));
    DelayCommand(7.0,Hover("0.90,0.80"));
    DelayCommand(8.0,VShot("c_released"));
    DelayCommand(9.0,Key("down"));
    DelayCommand(11.0,VShot("d_tab"));
    DelayCommand(12.0,Key("up"));
    DelayCommand(14.0,VShot("e_tab_released"));
    DelayCommand(15.0,VDone());
}
