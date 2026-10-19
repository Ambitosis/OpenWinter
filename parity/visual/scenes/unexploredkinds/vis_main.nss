#include "visual_inc"
// Which kinds of placeable stay drawn on unexplored tiles? Script-created banners and chests in the explored
// start room (F1, A) and in the unexplored west room (F2, B, plus a statue and a rug), before and after a reset.
void Note(string phase) {
 object pc=GetFirstPC();
 WriteTimestampedLogEntry("VISUAL|note|unexploredkinds|"+phase+"|startExplored="+IntToString(GetTileExplored(pc,GetArea(pc),4,1))
  +"|westExplored="+IntToString(GetTileExplored(pc,GetArea(pc),3,1)));
 VShot(phase);
}
void Explores(object pc,int on) { SetCreatureExploresMinimap(pc,on); }
void Plc(object a,string resref,float x,float y,string tag) { CreateObject(OBJECT_TYPE_PLACEABLE,resref,Location(a,Vector(x,y,0.0),90.0),FALSE,tag); }
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,60.0);SetPlotFlag(pc,TRUE);
 object a=GetArea(pc);
 Plc(a,"plc_flag5",44.0,13.0,"probe_f1");Plc(a,"plc_chest1",46.5,13.0,"probe_a");
 Plc(a,"plc_flag5",35.0,13.0,"probe_f2");Plc(a,"plc_chest1",35.0,17.0,"probe_b");
 Plc(a,"plc_statue1",32.5,15.0,"probe_s2");Plc(a,"plc_throwrug",36.5,15.0,"probe_r2");
 DelayCommand(2.0,VCamera(pc,90.0,30.0,60.0));DelayCommand(5.0,Note("a_start"));
 DelayCommand(6.0,Explores(pc,FALSE));DelayCommand(6.5,ExploreAreaForPlayer(a,pc,FALSE));DelayCommand(9.0,Note("b_reset"));
 DelayCommand(12.0,VDone());
}
