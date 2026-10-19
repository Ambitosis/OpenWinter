#include "visual_inc"
void Key(int k,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|"+state); }
void Reset(string clip) {
    object p=GetFirstPC(); VMovePC(p,40.0,40.0,0.0,0.0); VCamera(p,90.0,5.0,65.0);
    ReplaceObjectAnimation(p,"pauseturn",""); ReplaceObjectAnimation(p,"cturnr","");
    ReplaceObjectAnimation(p,"pausewalkl",""); ReplaceObjectAnimation(p,"pausewalkr","");
    ReplaceObjectAnimation(p,"cwalkl",""); ReplaceObjectAnimation(p,"cwalkr",""); ReplaceObjectAnimation(p,"cwalkb","");
    ReplaceObjectAnimation(p,clip,"sitcross");
}
void Trial(float t,string name,string clip,int key) {
    DelayCommand(t,Reset(clip)); DelayCommand(t+1.0,Key(key,"down"));
    DelayCommand(t+2.0,VShot(name)); DelayCommand(t+3.0,Key(key,"up"));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Trial(3.0,"a_pauseturn","pauseturn",65); Trial(8.0,"d_pauseturn","pauseturn",68);
    Trial(13.0,"a_cturnr","cturnr",65); Trial(18.0,"d_cturnr","cturnr",68);
    Trial(23.0,"q_pausewalkl","pausewalkl",81); Trial(28.0,"e_pausewalkr","pausewalkr",69);
    Trial(33.0,"q_cwalkl","cwalkl",81); Trial(38.0,"e_cwalkr","cwalkr",69);
    Trial(43.0,"s_cwalkb","cwalkb",83);
    DelayCommand(48.0,VDone());
}
