#include "visual_inc"
void Mouse(string xy,string state) { WriteTimestampedLogEntry("VISUAL|mouse|"+xy+"|"+state); }
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); VCamera(GetFirstPC(),90.0,12.0,55.0); }
void Trial(float t,string name,string xy) {
    DelayCommand(t,Reset()); DelayCommand(t+0.5,Mouse(xy,"down"));
    DelayCommand(t+1.7,VShot(name)); DelayCommand(t+2.1,Mouse(xy,"up"));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    ReplaceObjectAnimation(p,"walk","sitcross");
    Trial(3.0,"r16_walk_replaced","0.5125,0.60"); Trial(6.0,"r32_walk_replaced","0.525,0.60");
    Trial(9.0,"r48_walk_replaced","0.5375,0.60"); Trial(12.0,"r64_walk_replaced","0.55,0.60");
    DelayCommand(14.5,ReplaceObjectAnimation(p,"walk",""));
    Trial(15.0,"r128","0.6,0.60"); Trial(18.0,"r144","0.6125,0.60");
    Trial(21.0,"r160","0.625,0.60"); Trial(24.0,"r192","0.65,0.60");
    DelayCommand(27.0,VDone());
}
