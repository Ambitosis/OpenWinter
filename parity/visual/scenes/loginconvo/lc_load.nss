// The talker exists before the player logs in.
void main() {
 object a=GetObjectByTag("VISUAL_AREA");
 object n=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(a,Vector(11.0,8.0,0.0),180.0),FALSE,"probe_npc");
 ChangeToStandardFaction(n,STANDARD_FACTION_MERCHANT);SetPlotFlag(n,TRUE);
 WriteTimestampedLogEntry("VISUAL|note|loginconvo|load|area="+IntToString(GetIsObjectValid(a))+"|npc="+IntToString(GetIsObjectValid(n)));
}
