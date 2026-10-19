#include "visual_inc"
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_TILE_ACTION,"ta_event");
 WriteTimestampedLogEntry("VISUAL|note|fixture|act1="+Get2DAString("surfacemat","Act1_Strref",3)+"|act8="+Get2DAString("surfacemat","Act8_Strref",3)+"|surface="+IntToString(GetSurfaceMaterial(GetLocation(pc))));
 DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(24.0,VDone());
}
