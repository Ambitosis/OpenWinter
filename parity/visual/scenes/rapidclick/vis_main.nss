#include "visual_inc"
void Reset() { VMovePC(GetFirstPC(),60.0,60.0,0.0,0.0); VCamera(GetFirstPC(),90.0,12.0,55.0); }
void Click(string xy,string offset) { WriteTimestampedLogEntry("VISUAL|click|"+xy+"||"+offset); }
void Note(string name,float t) {
    object p=GetFirstPC(); vector v=GetPosition(p);
    WriteTimestampedLogEntry("VISUAL|note|"+name+"|"+FloatToString(t,0,2)+"|"+FloatToString(v.x,0,4)+","+FloatToString(v.y,0,4)
        +"|action="+IntToString(GetCurrentAction(p)));
}
void Trial(float t,string name,string xy,string offset) {
    DelayCommand(t,Reset());
    int i; for(i=0;i<6;i++) {
        DelayCommand(t+1.0+IntToFloat(i)*0.3,Click(xy,offset));
        DelayCommand(t+1.15+IntToFloat(i)*0.3,VShot(name+IntToString(i)));
    }
    DelayCommand(t+3.0,VShot(name+"coast")); DelayCommand(t+5.5,VShot(name+"idle"));
    for(i=0;i<46;i++) DelayCommand(t+1.0+IntToFloat(i)*0.1,Note(name,IntToFloat(i)*0.1));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Trial(3.0,"short","0.5375,0.60","0.70025,-0.04610");
    Trial(11.0,"medium","0.70,0.60","3.73468,-0.04610");
    Trial(19.0,"long","0.90,0.60","7.46936,-0.04610");
    DelayCommand(27.0,VDone());
}
