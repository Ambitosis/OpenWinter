#include "visual_inc"
// What interior tiles draw when minimap exploration is off, and right after the explored map is reset.
// Same room, door and camera as doorreveal.
object Door() { return GetNearestObjectToLocation(OBJECT_TYPE_DOOR,Location(GetArea(GetFirstPC()),Vector(40.0,15.0,0.0),0.0)); }
void Note(string phase) {
 object pc=GetFirstPC(); vector p=GetPosition(pc);
 string mask=""; int y; int x; for(y=0;y<10;y++){for(x=0;x<10;x++)mask+=IntToString(GetTileExplored(pc,GetArea(pc),x,y));mask+="/";}
 WriteTimestampedLogEntry("VISUAL|note|minimapoff|"+phase+"|pc="+FloatToString(p.x)+","+FloatToString(p.y)+"|explores="+IntToString(GetCreatureExploresMinimap(pc))+"|open="+IntToString(GetIsOpen(Door()))+"|map="+mask);
 VShot(phase);
}
void Explores(object pc,int on) { SetCreatureExploresMinimap(pc,on); }
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCreatureExploresMinimap(pc,FALSE);
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,60.0);SetPlotFlag(pc,TRUE);
 object d=Door();SetLocked(d,FALSE);
 DelayCommand(2.0,VCamera(pc,90.0,28.0,25.0));DelayCommand(5.0,Note("a_off_start"));
 DelayCommand(7.0,AssignCommand(d,ActionOpenDoor(d)));DelayCommand(11.0,Note("b_off_open"));
 DelayCommand(13.0,VMovePC(pc,35.0,15.0,0.0,0.0));DelayCommand(15.0,VCamera(pc,90.0,28.0,25.0));DelayCommand(18.0,Note("c_off_entered"));
 DelayCommand(20.0,VMovePC(pc,45.0,15.0,0.0,180.0));DelayCommand(22.0,AssignCommand(d,ActionCloseDoor(d)));
 DelayCommand(23.0,VCamera(pc,90.0,28.0,25.0));DelayCommand(26.0,Note("d_off_back_closed"));
 DelayCommand(28.0,Explores(pc,TRUE));DelayCommand(32.0,Note("e_on"));
 DelayCommand(34.0,ExploreAreaForPlayer(GetArea(pc),pc,FALSE));DelayCommand(34.1,Note("f_reset_now"));
 DelayCommand(38.0,Note("g_reset_settled"));
 DelayCommand(40.0,Explores(pc,FALSE));DelayCommand(40.5,ExploreAreaForPlayer(GetArea(pc),pc,FALSE));
 DelayCommand(44.0,Note("h_off_reset"));DelayCommand(47.0,VDone());
}
