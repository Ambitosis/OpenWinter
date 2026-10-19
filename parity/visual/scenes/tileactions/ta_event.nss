void main(){
 object pc=GetLastPlayerToDoTileAction();vector v=GetLastTileActionPosition();
 int id=GetLastTileActionId();
 SetLocalInt(pc,"tile_last_id",id);SetLocalLocation(pc,"tile_last_pos",Location(GetArea(pc),v,0.0));SetLocalInt(pc,"tile_count",GetLocalInt(pc,"tile_count")+1);
 WriteTimestampedLogEntry("VISUAL|note|tile_event|id="+IntToString(id)+"|pc="+IntToString(GetIsPC(pc))+"|selfmodule="+IntToString(OBJECT_SELF==GetModule())+"|x="+FloatToString(v.x)+"|y="+FloatToString(v.y)+"|z="+FloatToString(v.z)+"|surface="+IntToString(GetSurfaceMaterial(Location(GetArea(pc),v,0.0)))+"|action="+IntToString(GetCurrentAction(pc))+"|event="+IntToString(GetCurrentlyRunningEvent()));
}
