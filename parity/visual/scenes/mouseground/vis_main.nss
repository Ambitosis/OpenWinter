#include "visual_inc"
void Target(string name) { SetLocalString(GetModule(),"phase",name); EnterTargetingMode(GetFirstPC()); }
void Click(string xy) { WriteTimestampedLogEntry("VISUAL|click|"+xy); }
void Trial(float t,string name,string xy) { DelayCommand(t,Target(name)); DelayCommand(t+0.3,Click(xy)); }
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetEventScript(GetModule(),3018,"vis_target");
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(2.0,VCamera(p,90.0,12.0,55.0));
    Trial(4.0,"r8","0.50625,0.60"); Trial(5.0,"r16","0.5125,0.60");
    Trial(6.0,"r32","0.525,0.60"); Trial(7.0,"r48","0.5375,0.60");
    Trial(8.0,"r64","0.55,0.60"); Trial(9.0,"r96","0.575,0.60");
    Trial(10.0,"r128","0.60,0.60"); Trial(11.0,"r144","0.6125,0.60");
    Trial(12.0,"r160","0.625,0.60"); Trial(13.0,"r192","0.65,0.60");
    DelayCommand(14.0,VCamera(p,90.0,6.0,55.0)); Trial(15.0,"zoom6r64","0.55,0.60");
    DelayCommand(17.0,VDone());
}
