#include "visual_inc"
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); }
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|tap"); }
void Burst(string name) { WriteTimestampedLogEntry("VISUAL|burst|"+name+"|1.4|15"); }
void Note(string name) {
    object p=GetFirstPC(); vector v=GetPosition(p);
    WriteTimestampedLogEntry("VISUAL|note|"+name+"|"+FloatToString(v.x,0,4)+","+FloatToString(v.y,0,4)
        +"|facing="+FloatToString(GetFacing(p),0,2)+"|action="+IntToString(GetCurrentAction(p)));
}
void Trial(float t,int key,string name) {
    DelayCommand(t,Reset()); DelayCommand(t+1.0,Tap(key)); DelayCommand(t+1.1,Burst(name));
    int i; for(i=0;i<16;i++) DelayCommand(t+1.0+IntToFloat(i)*0.1,Note(name+IntToString(i)));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(2.0,VCamera(p,90.0,4.5,65.0));
    Trial(3.0,81,"left"); Trial(8.0,69,"right");
    Trial(13.0,65,"turnleft"); Trial(18.0,68,"turnright");
    Trial(23.0,87,"forward"); Trial(28.0,83,"back");
    DelayCommand(33.0,VDone());
}
