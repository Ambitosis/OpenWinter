#include "visual_inc"
object Door() { return GetNearestObjectToLocation(OBJECT_TYPE_DOOR,Location(GetArea(GetFirstPC()),Vector(40.0,15.0,0.0),0.0)); }
void Note(string phase) {
 object pc=GetFirstPC(); object d=Door(); object n=GetObjectByTag("probe_npc"); vector p=GetPosition(pc); vector q=GetPosition(n);
 WriteTimestampedLogEntry("VISUAL|note|reveal|"+phase+"|pc="+FloatToString(p.x)+","+FloatToString(p.y)+"|door="+GetTag(d)+"|open="+IntToString(GetIsOpen(d))+"|npc="+FloatToString(q.x)+","+FloatToString(q.y)+"|seen="+IntToString(GetObjectSeen(n,pc))+"|heard="+IntToString(GetObjectHeard(n,pc))+"|reverseSeen="+IntToString(GetObjectSeen(pc,n))+"|reverseHeard="+IntToString(GetObjectHeard(pc,n))+"|los="+IntToString(LineOfSightObject(pc,n))+"|explored="+IntToString(GetTileExplored(pc,GetArea(pc),3,1)));
 string mask=""; int y; int x; for(y=0;y<10;y++){for(x=0;x<10;x++)mask+=IntToString(GetTileExplored(pc,GetArea(pc),x,y));mask+="/";}
 WriteTimestampedLogEntry("VISUAL|note|map|"+phase+"|"+mask);
 VShot(phase);
}
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,60.0);SetPlotFlag(pc,TRUE);
 object d=Door();SetLocked(d,FALSE);
 object n=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(35.0,15.0,0.0),0.0),FALSE,"probe_npc");
 SetAILevel(n,AI_LEVEL_NORMAL);
 ChangeToStandardFaction(n,STANDARD_FACTION_MERCHANT);SetPlotFlag(n,TRUE);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectCutsceneParalyze(),n);
 CreateObject(OBJECT_TYPE_PLACEABLE,"nw_plc_chestsm",Location(GetArea(pc),Vector(35.0,17.0,0.0),0.0),FALSE,"probe_chest");
 DelayCommand(2.0,VCamera(pc,90.0,28.0,25.0));DelayCommand(5.0,Note("a_never_opened"));
 DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|key|9|down"));DelayCommand(9.0,Note("b_closed_tab"));DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|key|9|up"));
 DelayCommand(12.0,AssignCommand(d,ActionOpenDoor(d)));DelayCommand(18.0,Note("c_open"));
 DelayCommand(19.0,AssignCommand(d,ActionCloseDoor(d)));DelayCommand(23.0,Note("d_closed_again"));
 DelayCommand(26.0,VCamera(pc,0.0,12.0,65.0));DelayCommand(29.0,Note("e_camera_other_side"));
 DelayCommand(32.0,VCamera(pc,180.0,12.0,65.0));DelayCommand(35.0,Note("f_camera_same_side"));
 DelayCommand(38.0,AssignCommand(d,ActionOpenDoor(d)));DelayCommand(41.0,VMovePC(pc,35.0,15.0,0.0,0.0));DelayCommand(43.0,VCamera(pc,90.0,28.0,25.0));DelayCommand(46.0,Note("g_entered"));
 DelayCommand(49.0,VMovePC(pc,45.0,15.0,0.0,180.0));DelayCommand(51.0,AssignCommand(d,ActionCloseDoor(d)));DelayCommand(54.0,Note("h_return_closed"));
 DelayCommand(57.0,ExploreAreaForPlayer(GetArea(pc),pc,TRUE));DelayCommand(60.0,Note("i_explore_all"));DelayCommand(63.0,VDone());
}
