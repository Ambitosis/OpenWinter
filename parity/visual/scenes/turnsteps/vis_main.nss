#include "visual_inc"
void Key(int k,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|"+state); }
void Burst(string name) { WriteTimestampedLogEntry("VISUAL|burst|"+name+"|1.0|8"); }
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); VCamera(GetFirstPC(),90.0,4.5,65.0); }
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(3.0,Reset()); DelayCommand(4.0,Key(65,"down"));
    DelayCommand(4.3,Burst("left")); DelayCommand(7.0,Key(65,"up")); DelayCommand(8.0,VShot("left_stopped"));
    DelayCommand(10.0,Reset()); DelayCommand(11.0,Key(68,"down"));
    DelayCommand(11.3,Burst("right")); DelayCommand(14.0,Key(68,"up")); DelayCommand(15.0,VShot("right_stopped"));
    DelayCommand(17.0,VDone());
}
