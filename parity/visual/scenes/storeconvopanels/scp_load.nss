// The talker and the store exist before the player arrives.
void main() {
 object a=GetObjectByTag("VISUAL_AREA");
 object n=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(a,Vector(11.0,8.0,0.0),180.0),FALSE,"probe_npc");
 ChangeToStandardFaction(n,STANDARD_FACTION_MERCHANT);SetPlotFlag(n,TRUE);
 CreateObject(OBJECT_TYPE_STORE,"nw_storgenral001",Location(a,Vector(10.0,8.0,0.0),0.0),FALSE,"probe_store");
}
