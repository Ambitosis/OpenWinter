#include "visual_inc"
// Saved heights 3 and 1 (settings.tml ui.chat.pane.primary/secondary): the window's height before the split,
// then "Split Chat Window" chosen with a held right button (the recipe that worked in chatmenuheld), then one kind
// of line at a time with a shot after each, to see which window takes it.
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Talk(object o, string s, int vol) { AssignCommand(o, SpeakString(s, vol)); }
void Hurt(object pc) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), pc); }
void Save(object pc) { FortitudeSave(pc, 12); }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(11.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
 ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); SetPlotFlag(n, TRUE); SetPlotFlag(pc, TRUE);
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 55.0));
 DelayCommand(3.0, VShot("a_heights"));
 DelayCommand(5.0, Log("mouse|0.85,0.25|down|right"));
 DelayCommand(5.5, Log("hover|0.281,0.822"));
 DelayCommand(6.1, Log("mouse|0.281,0.822|up|right"));
 DelayCommand(7.0, Log("mouse|0.281,0.822|down|right"));
 DelayCommand(7.8, VShot("b_menu"));
 DelayCommand(8.2, Log("hover|0.2797,0.6431"));
 DelayCommand(9.0, Log("mouse|0.2797,0.6431|up|right"));
 DelayCommand(9.3, Log("hover|off"));
 DelayCommand(10.5, VShot("c_split"));
 DelayCommand(11.0, Talk(n, "Talk line one.", TALKVOLUME_TALK));
 DelayCommand(12.0, VShot("d_talk"));
 DelayCommand(12.5, SendMessageToPC(pc, "Server message one."));
 DelayCommand(13.5, VShot("e_server"));
 DelayCommand(14.0, Hurt(pc));
 DelayCommand(15.0, VShot("f_damage"));
 DelayCommand(15.5, Save(pc));
 DelayCommand(16.5, VShot("g_save"));
 DelayCommand(17.0, Talk(n, "Shout one.", TALKVOLUME_SHOUT));
 DelayCommand(18.0, VShot("h_shout"));
 DelayCommand(18.5, FloatingTextStringOnCreature("Floating text one.", pc, FALSE));
 DelayCommand(19.5, VShot("i_floating"));
 DelayCommand(20.0, Talk(n, "Whisper one.", TALKVOLUME_WHISPER));
 DelayCommand(21.0, VShot("j_whisper"));
 DelayCommand(21.5, SendMessageToPC(pc, "Server message two."));
 DelayCommand(22.5, VShot("k_server"));
 DelayCommand(23.0, VDone());
}
