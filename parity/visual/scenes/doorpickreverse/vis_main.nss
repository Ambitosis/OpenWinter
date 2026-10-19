#include "visual_inc"
void Mouse(float x,float y,string state) { WriteTimestampedLogEntry("VISUAL|mouse|"+FloatToString(x/1280.0,0,6)+","+FloatToString(y/720.0,0,6)+"|"+state); }
void Click(float x,float y){Mouse(x,y,"up");DelayCommand(0.3,Mouse(x,y,"down"));DelayCommand(0.5,Mouse(x,y,"up"));}
void Note(string label){object pc=GetFirstPC();vector p=GetPosition(pc);WriteTimestampedLogEntry("VISUAL|note|pick|"+label+"|open="+IntToString(GetIsOpen(GetObjectByTag("probe_door")))+"|pc="+FloatToString(p.x)+","+FloatToString(p.y));VShot(label);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);SetPlotFlag(pc,TRUE);
 object d=GetObjectByTag("probe_door");SetLocked(d,FALSE);
 DelayCommand(2.0,VCamera(pc,90.0,10.0,65.0));DelayCommand(4.0,Mouse(650.0,500.0,"up"));DelayCommand(6.0,Note("a_faded_hover"));
 DelayCommand(8.0,Click(650.0,500.0));DelayCommand(12.0,Note("b_faded_click"));
 DelayCommand(14.0,VMovePC(pc,20.0,23.0,0.0,270.0));DelayCommand(16.0,VCamera(pc,270.0,10.0,65.0));DelayCommand(18.0,Mouse(650.0,250.0,"up"));DelayCommand(20.0,Note("c_solid_hover"));
 DelayCommand(22.0,Click(650.0,250.0));DelayCommand(27.0,Note("d_solid_click"));
 DelayCommand(30.0,VDone());}
