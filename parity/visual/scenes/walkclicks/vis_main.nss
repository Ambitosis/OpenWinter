#include "visual_inc"
void Key(int k,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|"+state); }
void Click() { WriteTimestampedLogEntry("VISUAL|click|0.75,0.40"); }
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); VCamera(GetFirstPC(),90.0,10.0,55.0); }
void Measure(string label) {
    vector p=GetPosition(GetFirstPC());
    WriteTimestampedLogEntry("VISUAL|note|walkclick|"+label+"|"+FloatToString(p.x,0,4)+"|"+FloatToString(p.y,0,4));
}
void Trial(float t,string label,int walk) {
    DelayCommand(t,Reset());
    DelayCommand(t+0.5,Key(16,walk ? "down" : "up"));
    int i; for(i=0;i<8;i++) DelayCommand(t+1.0+IntToFloat(i)*0.15,Click());
    DelayCommand(t+2.4,Measure(label+"_a"));
    DelayCommand(t+2.9,Measure(label+"_b"));
    DelayCommand(t+3.0,VShot(label));
}
void main() {
    object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN); SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    Trial(2.0,"initial_run",FALSE);
    Trial(8.0,"shift_clicks",TRUE);
    Trial(14.0,"released_run",FALSE);
    DelayCommand(20.0,Key(16,"up"));
    DelayCommand(21.0,VDone());
}
