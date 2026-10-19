#include "visual_inc"
// Where the camera-side fade switches as the player crosses an open door's plane (y=40), from both sides.
void Pos(string name) {
 object pc=GetFirstPC(); vector p=GetPosition(pc);
 WriteTimestampedLogEntry("VISUAL|note|doorfaredge|"+name+"|pc="+FloatToString(p.x)+","+FloatToString(p.y));
 VShot(name);
}
void At(object pc,float t,float y,float camDir,string name) {
 DelayCommand(t,VMovePC(pc,40.0,y,0.0,90.0));DelayCommand(t+1.0,VCamera(pc,camDir,12.0,70.0));DelayCommand(t+3.0,Pos(name));
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);SetPlotFlag(pc,TRUE);
 object d=GetObjectByTag("probe_door");SetLocked(d,FALSE);
 DelayCommand(1.0,AssignCommand(d,ActionOpenDoor(d)));
 // Camera north of the door (as in doorfar h..m).
 At(pc,3.0,40.2,270.0,"n_y402");At(pc,7.0,40.3,270.0,"n_y403");At(pc,11.0,40.4,270.0,"n_y404");
 // Camera south of the door: the player starts north (opposite) and walks south across the plane.
 At(pc,15.0,40.5,90.0,"s_y405");At(pc,19.0,40.1,90.0,"s_y401");At(pc,23.0,39.9,90.0,"s_y399");
 At(pc,27.0,39.8,90.0,"s_y398");At(pc,31.0,39.7,90.0,"s_y397");At(pc,35.0,39.6,90.0,"s_y396");
 At(pc,39.0,39.5,90.0,"s_y395");At(pc,43.0,39.0,90.0,"s_y390");
 DelayCommand(48.0,VDone());
}
