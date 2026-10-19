#include "visual_inc"
void View(object pc,float t,float dir,string name) { DelayCommand(t,VCamera(pc,dir,10.0,65.0));DelayCommand(t+2.0,VShot(name)); }
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);SetPlotFlag(pc,TRUE);
 object d=GetObjectByTag("probe_door");SetLocked(d,FALSE);
 View(pc,2.0,90.0,"a_same");View(pc,6.0,270.0,"b_opposite");
 DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|key|9|down"));DelayCommand(11.0,VShot("c_opposite_tab"));DelayCommand(12.0,WriteTimestampedLogEntry("VISUAL|key|9|up"));
 View(pc,14.0,180.0,"d_tangent");View(pc,18.0,225.0,"e_oblique");
 DelayCommand(21.0,VMovePC(pc,24.0,17.0,0.0,90.0));View(pc,23.0,270.0,"f_offset");
 DelayCommand(26.0,VMovePC(pc,20.0,23.0,0.0,270.0));View(pc,28.0,90.0,"g_reverse_opposite");View(pc,32.0,270.0,"h_reverse_same");
 DelayCommand(35.0,AssignCommand(d,ActionOpenDoor(d)));View(pc,37.0,90.0,"i_open_opposite");View(pc,41.0,270.0,"j_open_same");
 DelayCommand(44.0,VMovePC(pc,20.0,17.0,0.0,90.0));View(pc,46.0,270.0,"k_open_front_opposite");
 DelayCommand(49.0,AssignCommand(d,ActionCloseDoor(d)));View(pc,51.0,270.0,"l_closed_front_opposite");
 DelayCommand(54.0,DestroyObject(d));DelayCommand(57.0,VShot("m_background"));DelayCommand(60.0,VDone());
}
