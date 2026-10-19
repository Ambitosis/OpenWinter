#include "visual_inc"
void Reset() { VMovePC(GetFirstPC(),60.0,60.0,0.0,0.0); }
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|tap"); }
// One shot per fresh tap: screenshot readback cannot stall the preceding animation.
void Trial(float t,int key,string name,float sample) {
    DelayCommand(t,Reset()); DelayCommand(t+0.5,Tap(key));
    DelayCommand(t+0.5+sample,VShot(name));
}
void Samples(float t,int key,string name) {
    Trial(t,key,name+"_300",0.30); Trial(t+1.8,key,name+"_400",0.40);
    Trial(t+3.6,key,name+"_500",0.50); Trial(t+5.4,key,name+"_600",0.60);
    Trial(t+7.2,key,name+"_700",0.70); Trial(t+9.0,key,name+"_850",0.85);
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(1.0,VCamera(p,90.0,7.0,60.0));
    Samples(3.0,87,"forward"); Samples(14.0,83,"back");
    Samples(25.0,81,"left"); Samples(36.0,69,"right");
    Samples(47.0,65,"turnleft"); Samples(58.0,68,"turnright");
    DelayCommand(70.0,VDone());
}
