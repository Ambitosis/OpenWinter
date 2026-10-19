#include "visual_inc"
// Every way a conversation can stop, one case at a time: which of the dialog's end and abort scripts runs, and
// what reaches the chat window (the harness logs both engines' chat windows).
object Npc() { return GetObjectByTag("probe_npc"); }
void Note(string phase) {
 object pc=GetFirstPC(); object n=Npc();
 WriteTimestampedLogEntry("VISUAL|note|convoend|"+GetLocalString(GetModule(),"case")+"|"+phase+"|pcTalking="+IntToString(IsInConversation(pc))
  +"|npcTalking="+IntToString(IsInConversation(n))+"|dist="+FloatToString(GetDistanceBetween(pc,n),0,2));
}
void Log(string s) { WriteTimestampedLogEntry("VISUAL|"+s); }
void Case(string name) { SetLocalString(GetModule(),"case",name); Note("begin"); }
void Start(string dlg) { AssignCommand(Npc(),ActionStartConversation(GetFirstPC(),dlg,FALSE,FALSE)); }
void JumpPc(float x, float y) { object pc=GetFirstPC(); AssignCommand(pc,ClearAllActions(TRUE)); AssignCommand(pc,JumpToLocation(Location(GetArea(pc),Vector(x,y,0.0),0.0))); }
void JumpNpc(float x, float y) { object n=Npc(); AssignCommand(n,ClearAllActions(TRUE)); AssignCommand(n,JumpToLocation(Location(GetArea(n),Vector(x,y,0.0),180.0))); }
void WalkPc(float x, float y) { object pc=GetFirstPC(); AssignCommand(pc,ActionMoveToLocation(Location(GetArea(pc),Vector(x,y,0.0),0.0))); }
void Hurt() { ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(1),GetFirstPC()); }
void Clear() { AssignCommand(GetFirstPC(),ClearAllActions(TRUE)); }
void Gone() { SetPlotFlag(Npc(),FALSE); DestroyObject(Npc()); }
void Respawn() {
 if(GetIsObjectValid(Npc()))return;
 object n=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(GetFirstPC()),Vector(11.0,8.0,0.0),180.0),FALSE,"probe_npc");
 ChangeToStandardFaction(n,STANDARD_FACTION_MERCHANT);SetPlotFlag(n,TRUE);
}
void Reset() { Respawn(); JumpPc(8.0,8.0); JumpNpc(11.0,8.0); }
// Talking state every half second while the case runs (the walk-away distance, when a walk ends it).
void Poll(int k) { Note("t"+IntToString(k)); if(k<14)DelayCommand(0.5,Poll(k+1)); }

void StoreCase(string name, string dlg, int keys, float t) {
 DelayCommand(t,Case(name));DelayCommand(t,Start(dlg));DelayCommand(t+0.5,Poll(0));
 if(keys>0)DelayCommand(t+2.0,Log("key|49|tap"));
 DelayCommand(t+4.0,VShot(name));DelayCommand(t+8.0,JumpPc(20.0,8.0));DelayCommand(t+9.5,Reset());
}
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,55.0));
 float t=4.0;
 // Repeats: the store opened by the first line (one ending answer), and by later lines.
 StoreCase("rep_first_end","ce_store1",0,t); t+=13.0;
 StoreCase("rep_tail_end","ce_ts1",1,t); t+=13.0;
 StoreCase("rep_tail_none","ce_ts0",1,t); t+=13.0;
 StoreCase("rep_first_end2","ce_store1",0,t); t+=13.0;
 // The store opened half a second after the line shows.
 StoreCase("late_first_end","ce_late1",0,t); t+=13.0;
 StoreCase("late_first_on","ce_late2",0,t); t+=13.0;
 StoreCase("late_tail_none","ce_late0",1,t); t+=13.0;
 // No store: a last line with an action script, jumped away from; a later line with one ending answer, Esc.
 DelayCommand(t,Case("lastact_jump"));DelayCommand(t,Start("ce_lastact"));DelayCommand(t+2.0,JumpPc(34.0,30.0));DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 DelayCommand(t,Case("lastlater_esc"));DelayCommand(t,Start("ce_lastlater"));DelayCommand(t+2.0,Log("key|49|tap"));DelayCommand(t+3.5,Log("key|27|tap"));DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 DelayCommand(t,VDone());
}
