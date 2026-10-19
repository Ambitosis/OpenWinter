#include "visual_inc"
// The Academy's storeroom (Storage 2 1x2 group: crate-filled tic01_c07_01 at tile (8,4), its walkable half
// tic01_c06_01 at (9,4)). Logs the 12x12 explored mask and views the crate half from the walkable half.
void Note(string name) {
 object pc=GetFirstPC(); string mask=""; int y; int x;
 for(y=0;y<12;y++){for(x=0;x<12;x++)mask+=IntToString(GetTileExplored(pc,GetArea(pc),x,y));mask+="/";}
 WriteTimestampedLogEntry("VISUAL|note|map|"+name+"|"+mask);VShot(name);
}
void Reset(object pc) { ExploreAreaForPlayer(GetArea(pc),pc,FALSE); }
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
 DelayCommand(2.0,Reset(pc));
 DelayCommand(3.0,VCamera(pc,180.0,14.0,50.0));DelayCommand(6.0,Note("a_west"));
 DelayCommand(8.0,VCamera(pc,0.0,14.0,50.0));DelayCommand(11.0,Note("b_east"));
 DelayCommand(13.0,VCamera(pc,270.0,18.0,30.0));DelayCommand(16.0,Note("c_top"));
 DelayCommand(18.0,VMovePC(pc,96.0,51.5,0.0,270.0));DelayCommand(19.0,Reset(pc));
 DelayCommand(20.0,VCamera(pc,180.0,14.0,50.0));DelayCommand(23.0,Note("d_corridor"));
 DelayCommand(25.0,VMovePC(pc,91.0,45.5,0.0,180.0));DelayCommand(26.0,Reset(pc));
 DelayCommand(27.0,VCamera(pc,135.0,12.0,55.0));DelayCommand(30.0,Note("e_seam"));
 DelayCommand(33.0,VDone());
}
