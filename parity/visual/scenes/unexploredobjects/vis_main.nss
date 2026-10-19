#include "visual_inc"
// Which objects the original draws on unexplored interior tiles. The room west of the door at x=40 starts
// unexplored; a chest, a creature and an item lie in it.
object Door() { return GetNearestObjectToLocation(OBJECT_TYPE_DOOR,Location(GetArea(GetFirstPC()),Vector(40.0,15.0,0.0),0.0)); }
void Note(string phase) {
 object pc=GetFirstPC(); object n=GetObjectByTag("probe_npc");
 WriteTimestampedLogEntry("VISUAL|note|unexploredobjects|"+phase+"|westExplored="+IntToString(GetTileExplored(pc,GetArea(pc),3,1))
  +"|open="+IntToString(GetIsOpen(Door()))+"|npcSeen="+IntToString(GetObjectSeen(n,pc))+"|npcHeard="+IntToString(GetObjectHeard(n,pc)));
 VShot(phase);
}
void Explores(object pc,int on) { SetCreatureExploresMinimap(pc,on); }
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,60.0);SetPlotFlag(pc,TRUE);
 object d=Door();SetLocked(d,FALSE);
 object a=GetArea(pc);
 object n=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(a,Vector(35.0,13.0,0.0),0.0),FALSE,"probe_npc");
 ChangeToStandardFaction(n,STANDARD_FACTION_MERCHANT);SetPlotFlag(n,TRUE);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectCutsceneParalyze(),n);
 CreateObject(OBJECT_TYPE_PLACEABLE,"nw_plc_chestsm",Location(a,Vector(35.0,17.0,0.0),0.0),FALSE,"probe_chest");
 CreateObject(OBJECT_TYPE_PLACEABLE,"plc_barrel",Location(a,Vector(33.0,15.0,0.0),0.0),FALSE,"probe_barrel");
 CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",Location(a,Vector(36.5,15.0,0.0),0.0),FALSE,"probe_item");
 DelayCommand(2.0,VCamera(pc,90.0,25.0,40.0));DelayCommand(5.0,Note("a_closed_unexplored"));
 DelayCommand(6.0,Explores(pc,FALSE));
 DelayCommand(7.0,AssignCommand(d,ActionOpenDoor(d)));DelayCommand(11.0,Note("b_open_unexplored"));
 DelayCommand(12.0,AssignCommand(d,ActionCloseDoor(d)));
 DelayCommand(14.0,ExploreAreaForPlayer(a,pc,FALSE));DelayCommand(17.0,Note("c_all_unexplored"));
 DelayCommand(18.0,VCamera(pc,90.0,40.0,70.0));DelayCommand(21.0,Note("d_all_unexplored_high"));
 DelayCommand(22.0,ExploreAreaForPlayer(a,pc,TRUE));DelayCommand(25.0,Note("e_all_explored_high"));
 DelayCommand(28.0,VDone());
}
