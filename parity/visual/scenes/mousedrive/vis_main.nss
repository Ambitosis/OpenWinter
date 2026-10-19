#include "visual_inc"
void Mouse(string xy,string state) { WriteTimestampedLogEntry("VISUAL|mouse|"+xy+"|"+state); }
void Note(string name,float t) {
    object p=GetFirstPC(); vector v=GetPosition(p);
    WriteTimestampedLogEntry("VISUAL|note|"+name+"|"+FloatToString(t,0,2)+"|"+FloatToString(v.x,0,4)+","+FloatToString(v.y,0,4)+"|"+FloatToString(GetFacing(p),0,3));
}
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); VCamera(GetFirstPC(),90.0,12.0,55.0); }
void Trial(float t,string name,string xy) {
    DelayCommand(t,Reset()); DelayCommand(t+1.0,Mouse(xy,"down"));
    int i=0; while(i<=35) { DelayCommand(t+0.8+IntToFloat(i)*0.1,Note(name,IntToFloat(i)*0.1)); i++; }
    DelayCommand(t+2.0,VShot(name)); DelayCommand(t+3.5,Mouse(xy,"up"));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Trial(3.0,"east_near","0.55,0.60"); Trial(9.0,"east_far","0.75,0.60");
    Trial(15.0,"north_near","0.5,0.53"); Trial(21.0,"north_far","0.5,0.3");
    Trial(27.0,"south_near","0.5,0.65"); Trial(33.0,"south_far","0.5,0.78");
    Trial(39.0,"west_near","0.45,0.60"); Trial(45.0,"west_far","0.25,0.60");
    DelayCommand(51.0,VDone());
}
