#include "visual_inc"
void Note(string name) {
 object door=GetObjectByTag("M1Q0B_M1Q0A");
 WriteTimestampedLogEntry("VISUAL|note|entry|"+name+"|valid="+IntToString(GetIsObjectValid(door))+"|open="+IntToString(GetIsOpen(door)));VShot(name);
}
void View(object pc,float t,float angle,string name) {
 DelayCommand(t,VCamera(pc,angle,18.0,55.0));DelayCommand(t+2.0,Note(name));
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
 DelayCommand(4.0,VBegin(pc));DelayCommand(4.0,VHidePC(pc));
 View(pc,6.0,45.0,"entry_045");
 View(pc,10.0,135.0,"entry_135");
 View(pc,14.0,225.0,"entry_225");
 View(pc,18.0,315.0,"entry_315");
 DelayCommand(23.0,VDone());
}
