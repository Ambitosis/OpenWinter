#include "visual_inc"
// The explored map tile by tile at the start of the Prelude's training hall (one note row per tile row, north last).
void Mask(string phase) {
 object pc=GetFirstPC(); object a=GetArea(pc);
 int w=GetAreaSize(AREA_WIDTH,a), h=GetAreaSize(AREA_HEIGHT,a), x, y;
 for(y=0;y<h;y++){ string row=""; for(x=0;x<w;x++) row+=IntToString(GetTileExplored(pc,a,x,y));
  WriteTimestampedLogEntry("VISUAL|note|exploremask|"+phase+"|y"+IntToString(y)+"|"+row); }
}
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,60.0);
 DelayCommand(2.0,VCamera(pc,90.0,30.0,60.0));DelayCommand(5.0,Mask("start"));DelayCommand(5.2,VShot("a_start"));
 DelayCommand(9.0,Mask("later"));DelayCommand(10.0,VDone());
}
