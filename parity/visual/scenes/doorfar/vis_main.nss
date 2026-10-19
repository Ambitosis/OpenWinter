#include "visual_inc"
// Does the camera-side door fade reach doors far to the side of the player, and what happens in the doorway?
// The door's plane is y=40; the player stands south of it, the camera north (opposite) or south (same).
void Pos(string name) {
 object pc=GetFirstPC(); vector p=GetPosition(pc);
 WriteTimestampedLogEntry("VISUAL|note|doorfar|"+name+"|pc="+FloatToString(p.x)+","+FloatToString(p.y)+"|open="+IntToString(GetIsOpen(GetObjectByTag("probe_door"))));
 VShot(name);
}
void Pair(object pc,float t,float x,float y,string name) {
 DelayCommand(t,VMovePC(pc,x,y,0.0,90.0));
 DelayCommand(t+1.0,VCamera(pc,270.0,30.0,75.0));DelayCommand(t+3.0,Pos(name+"_opposite"));
 DelayCommand(t+4.0,VCamera(pc,90.0,30.0,75.0));DelayCommand(t+6.0,Pos(name+"_same"));
}
void Edge(object pc,float t,float y,string name) {
 DelayCommand(t,VMovePC(pc,40.0,y,0.0,90.0));DelayCommand(t+1.0,VCamera(pc,270.0,12.0,70.0));DelayCommand(t+3.0,Pos(name));
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);SetPlotFlag(pc,TRUE);
 object d=GetObjectByTag("probe_door");SetLocked(d,FALSE);
 Pair(pc,2.0,40.0,37.0,"a00");Pair(pc,9.0,44.0,37.0,"b04");Pair(pc,16.0,48.0,37.0,"c08");
 Pair(pc,23.0,52.0,37.0,"d12");Pair(pc,30.0,56.0,37.0,"e16");Pair(pc,37.0,64.0,37.0,"f24");
 Pair(pc,44.0,40.0,25.0,"g_back15");
 DelayCommand(51.0,AssignCommand(d,ActionOpenDoor(d)));
 Edge(pc,54.0,38.5,"h_y385");Edge(pc,58.0,39.5,"i_y395");Edge(pc,62.0,39.9,"j_y399");
 Edge(pc,66.0,40.1,"k_y401");Edge(pc,70.0,40.5,"l_y405");Edge(pc,74.0,41.5,"m_y415");
 DelayCommand(78.0,DestroyObject(d));DelayCommand(80.0,VMovePC(pc,40.0,37.0,0.0,90.0));
 DelayCommand(81.0,VCamera(pc,270.0,30.0,75.0));DelayCommand(83.0,VShot("n_background"));DelayCommand(86.0,VDone());
}
