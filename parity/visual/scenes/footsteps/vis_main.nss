#include "visual_inc"
void Key(int k,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|"+state); }
void Type(int type) {
    object p=GetFirstPC(); SetFootstepType(type,p);
    WriteTimestampedLogEntry("VISUAL|note|footstep|"+IntToString(GetFootstepType(p))+"|surface="+IntToString(GetSurfaceMaterial(GetLocation(p))));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(2.0,VCamera(p,90.0,7.0,65.0));
    DelayCommand(3.0,Type(0)); DelayCommand(3.2,Key(87,"down")); DelayCommand(5.0,Key(87,"up"));
    DelayCommand(6.0,Type(3)); DelayCommand(6.2,Key(81,"down")); DelayCommand(8.0,Key(81,"up"));
    DelayCommand(9.0,Type(12)); DelayCommand(9.2,Key(87,"down")); DelayCommand(11.0,Key(87,"up"));
    DelayCommand(12.0,Type(4)); DelayCommand(12.2,Key(65,"down")); DelayCommand(14.0,Key(65,"up"));
    DelayCommand(15.0,VShot("stopped")); DelayCommand(16.0,VDone());
}
