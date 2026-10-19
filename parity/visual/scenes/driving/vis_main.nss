#include "visual_inc"
void Key(int k,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|"+state); }
void Note(string name,float t) {
    object p=GetFirstPC(); vector v=GetPosition(p);
    WriteTimestampedLogEntry("VISUAL|note|"+name+"|"+FloatToString(t,0,2)+"|"+FloatToString(v.x,0,4)+","+FloatToString(v.y,0,4)+"|"+FloatToString(GetFacing(p),0,3));
}
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); VCamera(GetFirstPC(),90.0,12.0,55.0); }
void Trial(float t,string name,int key,int second=0) {
    DelayCommand(t,Reset());
    DelayCommand(t+1.0,Key(key,"down"));
    if(second>0) DelayCommand(t+1.0,Key(second,"down"));
    int i=0; while(i<=20) { DelayCommand(t+0.8+IntToFloat(i)*0.1,Note(name,IntToFloat(i)*0.1)); i++; }
    DelayCommand(t+2.0,VShot(name));
    DelayCommand(t+2.5,Key(key,"up"));
    if(second>0) DelayCommand(t+2.5,Key(second,"up"));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Trial(3.0,"w",87); Trial(8.0,"s",83); Trial(13.0,"a",65); Trial(18.0,"d",68);
    Trial(23.0,"q",81); Trial(28.0,"e",69);
    Trial(33.0,"aq",65,81); Trial(38.0,"de",68,69);
    Trial(43.0,"ae",65,69); Trial(48.0,"dq",68,81);
    Trial(53.0,"wa",87,65); Trial(58.0,"we",87,69);
    DelayCommand(63.0,VDone());
}
