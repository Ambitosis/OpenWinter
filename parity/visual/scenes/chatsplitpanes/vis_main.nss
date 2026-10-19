#include "visual_inc"
// The original's chat window started split (settings.tml ui.chat.pane.primary/secondary heights 2 and 6):
// one kind of line at a time, a shot after each, to see which window takes it and how tall each window is.
void Talk(object o, string s, int vol) { AssignCommand(o, SpeakString(s, vol)); }
void Hurt(object pc) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), pc); }
void Save(object pc) { FortitudeSave(pc, 12); }
void Fight(object pc, object n) { AssignCommand(n, ActionAttack(pc)); }
void Calm(object n) { AssignCommand(n, ClearAllActions(TRUE)); }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(11.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
 ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); SetPlotFlag(n, TRUE); SetPlotFlag(pc, TRUE);
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 55.0));
 DelayCommand(2.5, VShot("a_start"));
 DelayCommand(3.0, Talk(n, "Talk line one.", TALKVOLUME_TALK));
 DelayCommand(4.0, VShot("b_talk"));
 DelayCommand(4.5, SendMessageToPC(pc, "Server message one."));
 DelayCommand(5.5, VShot("c_server"));
 DelayCommand(6.0, Hurt(pc));
 DelayCommand(7.0, VShot("d_damage"));
 DelayCommand(7.5, Save(pc));
 DelayCommand(8.5, VShot("e_save"));
 DelayCommand(9.0, Talk(n, "Shout one.", TALKVOLUME_SHOUT));
 DelayCommand(10.0, VShot("f_shout"));
 DelayCommand(10.5, Talk(pc, "Player talk one.", TALKVOLUME_TALK));
 DelayCommand(11.5, VShot("g_player"));
 DelayCommand(12.0, Talk(n, "Whisper one.", TALKVOLUME_WHISPER));
 DelayCommand(13.0, VShot("h_whisper"));
 DelayCommand(13.5, FloatingTextStringOnCreature("Floating text one.", pc, FALSE));
 DelayCommand(14.5, VShot("i_floating"));
 DelayCommand(15.0, Fight(pc, n));
 DelayCommand(20.0, Calm(n));
 DelayCommand(21.0, VShot("j_fight"));
 DelayCommand(21.5, SendMessageToPC(pc, "Server message two."));
 DelayCommand(22.5, VShot("k_server"));
 DelayCommand(23.0, VDone());
}
