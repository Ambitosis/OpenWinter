#include "visual_inc"
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); }
void Key(int key,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|"+state); }
void Note(string name) {
    vector v=GetPosition(GetFirstPC());
    WriteTimestampedLogEntry("VISUAL|note|"+name+"|"+FloatToString(v.x,0,4)+","+FloatToString(v.y,0,4));
}
void Trial(float t,int key,float duration,string name) {
    DelayCommand(t,Reset()); DelayCommand(t+1.0,Key(key,"down")); DelayCommand(t+1.0+duration,Key(key,"up"));
    int i; for(i=0;i<21;i++) DelayCommand(t+1.0+IntToFloat(i)*0.1,Note(name+IntToString(i)));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN);
    Trial(3.0,81,0.65,"left"); Trial(8.0,87,0.55,"forward");
    Trial(13.0,83,0.55,"back"); Trial(18.0,87,0.25,"shortforward");
    DelayCommand(23.0,VDone());
}
