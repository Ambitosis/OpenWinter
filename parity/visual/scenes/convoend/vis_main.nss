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

void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,55.0));
 float t=4.0;
 // 0: the player answers to the end of the tree with the number keys (control: the end script).
 DelayCommand(t,Case("reply_end"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,VShot("reply_end_first"));
 DelayCommand(t+3.0,Log("key|49|tap"));DelayCommand(t+4.5,VShot("reply_end_second"));DelayCommand(t+5.0,Log("key|49|tap"));
 DelayCommand(t+6.5,VShot("reply_end_after"));DelayCommand(t+7.0,Note("after"));DelayCommand(t+9.0,Reset());
 t+=12.0;
 // 1: Esc while a line shows.
 DelayCommand(t,Case("esc"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,Log("key|27|tap"));DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 // 2: the player is moved 6.7 m off by a script.
 DelayCommand(t,Case("jump_near"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,JumpPc(8.0,14.0));DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 // 3: ... and 33 m off.
 DelayCommand(t,Case("jump_far"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,JumpPc(34.0,30.0));DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 // 4: a scripted walk away.
 DelayCommand(t,Case("walk_away"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,WalkPc(34.0,30.0));DelayCommand(t+2.0,Poll(0));DelayCommand(t+10.0,Reset());
 t+=13.0;
 // 5: the player clicks the ground (posted to the window, top middle).
 DelayCommand(t,Case("click_ground"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,Log("click|0.5,0.12"));DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 // 6: the talker is moved 26 m off.
 DelayCommand(t,Case("npc_jump"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,JumpNpc(34.0,20.0));DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 // 7: the talker is destroyed.
 DelayCommand(t,Case("npc_gone"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,Gone());DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 // 8: ClearAllActions(TRUE) on the player.
 DelayCommand(t,Case("clear_actions"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,Clear());DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 // 9: the player takes damage.
 DelayCommand(t,Case("damaged"));DelayCommand(t,Start("ce_talk"));DelayCommand(t+2.0,Hurt());DelayCommand(t+2.0,Poll(0));DelayCommand(t+9.0,Reset());
 t+=12.0;
 // 10: a line's action script opens the store and the line has no replies.
 DelayCommand(t,Case("store_noreply"));DelayCommand(t,Start("ce_store0"));DelayCommand(t+0.5,Poll(0));DelayCommand(t+2.0,VShot("store_noreply"));
 DelayCommand(t+8.0,JumpPc(20.0,8.0));DelayCommand(t+9.5,Reset());
 t+=13.0;
 // 11: ... and the line has a reply.
 DelayCommand(t,Case("store_reply"));DelayCommand(t,Start("ce_store1"));DelayCommand(t+0.5,Poll(0));DelayCommand(t+2.0,VShot("store_reply"));
 DelayCommand(t+8.0,JumpPc(20.0,8.0));DelayCommand(t+9.5,Reset());
 t+=13.0;
 // 12: a reply's action script opens the store and the reply leads on to another line.
 DelayCommand(t,Case("store_fromreply"));DelayCommand(t,Start("ce_store2"));DelayCommand(t+2.0,Log("key|49|tap"));DelayCommand(t+2.0,Poll(0));
 DelayCommand(t+4.0,VShot("store_fromreply"));DelayCommand(t+8.0,JumpPc(20.0,8.0));DelayCommand(t+9.5,Reset());
 t+=13.0;
 DelayCommand(t,VDone());
}
