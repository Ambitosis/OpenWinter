#include "visual_inc"
void Key(int k,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|"+state); }
void Reset(string clip) {
    object p=GetFirstPC(); VMovePC(p,40.0,40.0,0.0,0.0); VCamera(p,90.0,5.0,65.0);
    ReplaceObjectAnimation(p,"walk",""); ReplaceObjectAnimation(p,"run",""); ReplaceObjectAnimation(p,"runfl",""); ReplaceObjectAnimation(p,"runfr","");
    ReplaceObjectAnimation(p,"pausewalkfl",""); ReplaceObjectAnimation(p,"pausewalkfr","");
    ReplaceObjectAnimation(p,clip,"sitcross");
}
void Trial(float t,string name,string clip,int key,int second,int walk) {
    DelayCommand(t,Reset(clip)); DelayCommand(t+0.5,Key(key,"down"));
    if(second>0) DelayCommand(t+0.5,Key(second,"down")); if(walk>0) DelayCommand(t+0.5,Key(16,"down"));
    DelayCommand(t+1.3,VShot(name)); DelayCommand(t+2.0,Key(key,"up"));
    if(second>0) DelayCommand(t+2.0,Key(second,"up")); if(walk>0) DelayCommand(t+2.0,Key(16,"up"));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Trial(3.0,"s_walk","walk",83,0,0);
    Trial(7.0,"wq_runfl","runfl",87,81,0); Trial(11.0,"we_runfr","runfr",87,69,0);
    Trial(15.0,"wq_pausewalkfl","pausewalkfl",87,81,1); Trial(19.0,"we_pausewalkfr","pausewalkfr",87,69,1);
    Trial(23.0,"wq_run","run",87,81,0); Trial(27.0,"we_run","run",87,69,0);
    Trial(31.0,"wq_walk","walk",87,81,1); Trial(35.0,"we_walk","walk",87,69,1);
    DelayCommand(39.0,VDone());
}
