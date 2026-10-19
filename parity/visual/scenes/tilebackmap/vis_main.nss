#include "visual_inc"
void Note(string name) {
 object pc=GetFirstPC(); string mask=""; int y; int x;
 for(y=0;y<10;y++){for(x=0;x<10;x++)mask+=IntToString(GetTileExplored(pc,GetArea(pc),x,y));mask+="/";}
 WriteTimestampedLogEntry("VISUAL|note|map|"+name+"|"+mask);VShot(name);
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
 DelayCommand(2.0,VCamera(pc,90.0,22.0,55.0));DelayCommand(6.0,Note("a_settled"));
 DelayCommand(8.0,VHidePC(pc));DelayCommand(11.0,Note("b_hidden_pc"));
 DelayCommand(13.0,VBegin(pc));DelayCommand(16.0,Note("c_cutscene"));
 DelayCommand(18.0,ExploreAreaForPlayer(GetArea(pc),pc,TRUE));DelayCommand(21.0,Note("d_known"));
 DelayCommand(23.0,ExploreAreaForPlayer(GetArea(pc),pc,FALSE));DelayCommand(26.0,Note("e_reset"));DelayCommand(29.0,VDone());
}
