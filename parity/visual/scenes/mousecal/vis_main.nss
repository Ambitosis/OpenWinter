#include "visual_inc"
void Mouse(string xy,string state,string ground) { WriteTimestampedLogEntry("VISUAL|mouse|"+xy+"|"+state+"|"+ground); }
void Note(string name,float t) {
    object p=GetFirstPC(); vector v=GetPosition(p);
    WriteTimestampedLogEntry("VISUAL|note|"+name+"|"+FloatToString(t,0,2)+"|"+FloatToString(v.x,0,4)+","+FloatToString(v.y,0,4)+"|"+FloatToString(GetFacing(p),0,3));
}
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); VCamera(GetFirstPC(),90.0,12.0,55.0); }
void Trial(float t,string name,string xy,string ground) {
    DelayCommand(t,Reset()); DelayCommand(t+0.5,Mouse(xy,"down",ground));
    int i=0; while(i<=15) { DelayCommand(t+0.5+IntToFloat(i)*0.1,Note(name,IntToFloat(i)*0.1)); i++; }
    DelayCommand(t+0.6,VShot(name+"_100")); DelayCommand(t+0.7,VShot(name+"_200"));
    DelayCommand(t+0.9,VShot(name+"_400")); DelayCommand(t+1.5,VShot(name+"_1000"));
    DelayCommand(t+1.8,Mouse(xy,"up",ground));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Trial(3.0,"r8","0.50625,0.60","0.11671,-0.04610"); Trial(6.0,"r16","0.5125,0.60","0.23342,-0.04610");
    Trial(9.0,"r32","0.525,0.60","0.46684,-0.04610"); Trial(12.0,"r48","0.5375,0.60","0.70025,-0.04610");
    Trial(15.0,"r64","0.55,0.60","0.93367,-0.04610"); Trial(18.0,"r96","0.575,0.60","1.40051,-0.04610");
    Trial(21.0,"zoom6r64","0.55,0.60","0.53859,0.72881"); DelayCommand(21.2,VCamera(p,90.0,6.0,55.0));
    Trial(24.0,"r128","0.60,0.60","1.86734,-0.04610");
    DelayCommand(27.0,VDone());
}
