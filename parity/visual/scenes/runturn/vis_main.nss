#include "visual_inc"
void Key(int key,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|"+state); }
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); VCamera(GetFirstPC(),90.0,7.0,55.0); }
void Burst(string name) { WriteTimestampedLogEntry("VISUAL|burst|"+name+"|2|8"); }
void Trial(float t,int turn,int walk,string name) {
    DelayCommand(t,Reset());
    if(walk) DelayCommand(t+0.8,Key(16,"down"));
    DelayCommand(t+1.0,Key(87,"down"));
    DelayCommand(t+1.5,Key(turn,"down"));
    DelayCommand(t+2.0,Burst(name));
    DelayCommand(t+4.5,Key(turn,"up"));
    DelayCommand(t+5.0,Key(87,"up"));
    if(walk) DelayCommand(t+5.1,Key(16,"up"));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Trial(3.0,65,FALSE,"runleft"); Trial(11.0,68,FALSE,"runright");
    Trial(19.0,65,TRUE,"walkleft"); Trial(27.0,68,TRUE,"walkright");
    DelayCommand(34.0,VDone());
}
