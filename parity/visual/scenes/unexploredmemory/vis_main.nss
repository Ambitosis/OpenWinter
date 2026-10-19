#include "visual_inc"
// Does the original keep drawing objects it has already shown once their tiles are unexplored again?
// A: chest in the explored start room. B: barrel in the unexplored west room. C, D: creatures in each.
// E: a chest created in the start room after the reset.
void Note(string phase) {
 object pc=GetFirstPC();
 WriteTimestampedLogEntry("VISUAL|note|unexploredmemory|"+phase+"|startExplored="+IntToString(GetTileExplored(pc,GetArea(pc),4,1))
  +"|westExplored="+IntToString(GetTileExplored(pc,GetArea(pc),3,1))
  +"|cSeen="+IntToString(GetObjectSeen(GetObjectByTag("probe_c"),pc))+"|dSeen="+IntToString(GetObjectSeen(GetObjectByTag("probe_d"),pc)));
 VShot(phase);
}
void Explores(object pc,int on) { SetCreatureExploresMinimap(pc,on); }
object Npc(object a,float x,float y,string tag) {
 object n=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(a,Vector(x,y,0.0),0.0),FALSE,tag);
 ChangeToStandardFaction(n,STANDARD_FACTION_MERCHANT);SetPlotFlag(n,TRUE);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectCutsceneParalyze(),n);
 return n;
}
void MakeE(object a) { CreateObject(OBJECT_TYPE_PLACEABLE,"nw_plc_chestsm",Location(a,Vector(46.5,17.0,0.0),0.0),FALSE,"probe_e"); }
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,60.0);SetPlotFlag(pc,TRUE);
 object a=GetArea(pc);
 CreateObject(OBJECT_TYPE_PLACEABLE,"nw_plc_chestsm",Location(a,Vector(47.0,13.0,0.0),0.0),FALSE,"probe_a");
 CreateObject(OBJECT_TYPE_PLACEABLE,"plc_barrel",Location(a,Vector(35.0,17.0,0.0),0.0),FALSE,"probe_b");
 Npc(a,43.0,13.0,"probe_c");Npc(a,35.0,13.0,"probe_d");
 DelayCommand(2.0,VCamera(pc,90.0,30.0,60.0));DelayCommand(5.0,Note("a_start"));
 DelayCommand(6.0,Explores(pc,FALSE));DelayCommand(6.5,ExploreAreaForPlayer(a,pc,FALSE));DelayCommand(9.0,Note("b_reset_after_start"));
 DelayCommand(10.0,ExploreAreaForPlayer(a,pc,TRUE));DelayCommand(13.0,Note("c_all_explored"));
 DelayCommand(14.0,ExploreAreaForPlayer(a,pc,FALSE));DelayCommand(17.0,Note("d_reset_after_all"));
 DelayCommand(18.0,MakeE(a));DelayCommand(21.0,Note("e_new_chest_unexplored"));
 DelayCommand(22.0,VMovePC(pc,45.0,16.0,0.0,180.0));DelayCommand(23.0,VCamera(pc,90.0,30.0,60.0));DelayCommand(26.0,Note("f_pc_moved"));
 DelayCommand(29.0,VDone());
}
