#include "visual_inc"
#include "cc_inc"
// Esc during a conversation while the player is in cutscene mode: whether the dialog closes, which of its end and
// abort scripts runs, whether the module's OnPlayerCancelCutscene runs and whether cutscene mode stays on. Each case
// ends by moving the player 33 m off (which ends any conversation left open) and back.
object Npc() { return GetObjectByTag("probe_npc"); }
void Log(string s) { WriteTimestampedLogEntry("VISUAL|"+s); }
void Esc() { Log("key|27|tap"); }
void Case(string name) { SetLocalString(GetModule(),"case",name); CcNote("begin"); }
void Cut(int left) { SetCutsceneMode(GetFirstPC(),TRUE,left); }
void Uncut() { SetCutsceneMode(GetFirstPC(),FALSE); }
void Hook(string script) { SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_CANCEL_CUTSCENE,script); }
void Start() { AssignCommand(Npc(),ActionStartConversation(GetFirstPC(),"cc_talk",FALSE,FALSE)); }
void Begin() { ExecuteScript("cc_begin",Npc()); }
void JumpPc(float x, float y) { object pc=GetFirstPC(); AssignCommand(pc,ClearAllActions(TRUE)); AssignCommand(pc,JumpToLocation(Location(GetArea(pc),Vector(x,y,0.0),0.0))); }
void Back() { JumpPc(8.0,8.0); }
void Finish() { SetLocalString(GetModule(),"case",GetLocalString(GetModule(),"case")+"/reset"); Uncut(); JumpPc(34.0,30.0); DelayCommand(1.0,Back()); }
void Poll(int k) { CcNote("t"+IntToString(k)); if(k<7)DelayCommand(0.5,Poll(k+1)); }

void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 object n=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(11.0,8.0,0.0),180.0),FALSE,"probe_npc");
 ChangeToStandardFaction(n,STANDARD_FACTION_MERCHANT);SetPlotFlag(n,TRUE);
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,55.0));
 float t=4.0;
 // 0: control, no cutscene mode: Esc at the first line.
 DelayCommand(t,Case("plain_esc"));DelayCommand(t,Start());DelayCommand(t+2.0,Esc());DelayCommand(t+2.0,Poll(0));DelayCommand(t+6.0,Finish());
 t+=9.0;
 // 1: cutscene mode, the talker's ActionStartConversation, Esc at the first line, Esc again 2.5 s later.
 DelayCommand(t,Case("cut_action_esc"));DelayCommand(t,Cut(FALSE));DelayCommand(t+0.2,Start());DelayCommand(t+1.8,VShot("cut_action_line"));
 DelayCommand(t+3.0,Esc());DelayCommand(t+3.0,Poll(0));DelayCommand(t+4.5,VShot("cut_action_after_esc"));DelayCommand(t+5.5,Esc());DelayCommand(t+7.0,CcNote("after_second"));DelayCommand(t+7.5,Finish());
 t+=10.5;
 // 2: cutscene mode, BeginConversation run on the talker, Esc at the first line.
 DelayCommand(t,Case("cut_begin_esc"));DelayCommand(t,Cut(FALSE));DelayCommand(t+0.2,Begin());
 DelayCommand(t+2.0,Esc());DelayCommand(t+2.0,Poll(0));DelayCommand(t+6.0,Finish());
 t+=9.0;
 // 3: cutscene mode with left clicks enabled, Esc at the first line.
 DelayCommand(t,Case("cut_left_esc"));DelayCommand(t,Cut(TRUE));DelayCommand(t+0.2,Start());
 DelayCommand(t+2.0,Esc());DelayCommand(t+2.0,Poll(0));DelayCommand(t+6.0,Finish());
 t+=9.0;
 // 4: cutscene mode and no OnPlayerCancelCutscene script, Esc at the first line.
 DelayCommand(t,Case("cut_nohook_esc"));DelayCommand(t,Hook(""));DelayCommand(t,Cut(FALSE));DelayCommand(t+0.2,Start());
 DelayCommand(t+2.0,Esc());DelayCommand(t+2.0,Poll(0));DelayCommand(t+4.0,VShot("cut_nohook_after_esc"));DelayCommand(t+6.0,Finish());DelayCommand(t+6.0,Hook("cc_cabort"));
 t+=9.0;
 // 5: cutscene mode, the player answers with the number key, then Esc at the last line.
 DelayCommand(t,Case("cut_answer"));DelayCommand(t,Cut(FALSE));DelayCommand(t+0.2,Start());
 DelayCommand(t+2.0,Log("key|49|tap"));DelayCommand(t+3.0,VShot("cut_answer_second"));DelayCommand(t+3.5,CcNote("answered"));DelayCommand(t+4.0,Esc());DelayCommand(t+4.0,Poll(0));DelayCommand(t+8.0,Finish());
 t+=11.0;
 // 6: cutscene mode ends by script while the line shows, then Esc.
 DelayCommand(t,Case("cut_then_off_esc"));DelayCommand(t,Cut(FALSE));DelayCommand(t+0.2,Start());DelayCommand(t+2.0,Uncut());
 DelayCommand(t+3.0,Esc());DelayCommand(t+3.0,Poll(0));DelayCommand(t+7.0,Finish());
 t+=10.0;
 // 7: cutscene mode and no conversation: Esc.
 DelayCommand(t,Case("cut_noconvo"));DelayCommand(t,Cut(FALSE));DelayCommand(t+1.0,Esc());DelayCommand(t+2.5,CcNote("after"));DelayCommand(t+3.0,Finish());
 t+=6.0;
 DelayCommand(t,VDone());
}
