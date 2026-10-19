#include "visual_inc"
// Items and a heard-only creature on unexplored tiles: an item shown in the explored start room, then the map
// reset (is it remembered like a placeable?); an item in the never-explored west room; and an invisible creature
// pacing beside the player, heard but not seen once the reset leaves every tile unexplored (does it still show as a faint shape on unexplored tiles?).
void Note(string phase) {
 object pc=GetFirstPC(); object g=GetObjectByTag("probe_ghost");
 WriteTimestampedLogEntry("VISUAL|note|unexploreditems|"+phase+"|startExplored="+IntToString(GetTileExplored(pc,GetArea(pc),4,1))
  +"|westExplored="+IntToString(GetTileExplored(pc,GetArea(pc),3,1))+"|ghostSeen="+IntToString(GetObjectSeen(g,pc))+"|ghostHeard="+IntToString(GetObjectHeard(g,pc)));
 VShot(phase);
}
void Explores(object pc,int on) { SetCreatureExploresMinimap(pc,on); }
void Item(object a,string resref,float x,float y,string tag) { CreateObject(OBJECT_TYPE_ITEM,resref,Location(a,Vector(x,y,0.0),0.0),FALSE,tag); }
void Pace(object g,object a) {
 AssignCommand(g,ActionMoveToLocation(Location(a,Vector(43.0,17.0,0.0),0.0)));
 AssignCommand(g,ActionMoveToLocation(Location(a,Vector(47.0,17.0,0.0),0.0)));
 AssignCommand(g,ActionDoCommand(Pace(g,a)));
}
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,60.0);SetPlotFlag(pc,TRUE);
 object a=GetArea(pc);
 Item(a,"nw_wswls001",44.0,13.0,"probe_i1");Item(a,"nw_aarcl004",46.5,13.0,"probe_i2");
 Item(a,"nw_wswls001",35.0,13.0,"probe_i3");Item(a,"nw_aarcl004",35.0,17.0,"probe_i4");
 object g=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(a,Vector(45.0,17.0,0.0),0.0),FALSE,"probe_ghost");
 ChangeToStandardFaction(g,STANDARD_FACTION_MERCHANT);SetPlotFlag(g,TRUE);
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectInvisibility(INVISIBILITY_TYPE_NORMAL),g);
 DelayCommand(1.0,Pace(g,a));
 DelayCommand(2.0,VCamera(pc,90.0,30.0,60.0));DelayCommand(5.0,Note("a_start"));
 DelayCommand(6.0,Explores(pc,FALSE));DelayCommand(6.5,ExploreAreaForPlayer(a,pc,FALSE));DelayCommand(9.0,Note("b_reset"));
 DelayCommand(12.0,Note("c_reset_later"));
 // Then the west room explored by script: the ghost and items there as the original draws them on explored tiles.
 DelayCommand(13.0,ExploreAreaForPlayer(a,pc,TRUE));DelayCommand(16.0,Note("d_all_explored"));
 DelayCommand(18.0,VDone());
}
