#include "visual_inc"
// The original's chat window split in two ("Split Chat Window"): which lines go to which pane.
void Talk(object o, string s, int vol) { AssignCommand(o, SpeakString(s, vol)); }
void Hurt(object pc) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), pc); }
void Lines(object pc, object n, string k) {
 Talk(n, "Talk line from the probe " + k + ".", TALKVOLUME_TALK);
 DelayCommand(0.3, Talk(pc, "Talk line from the player " + k + ".", TALKVOLUME_TALK));
 DelayCommand(0.6, Talk(n, "Shout from the probe " + k + ".", TALKVOLUME_SHOUT));
 DelayCommand(0.9, SendMessageToPC(pc, "Server message " + k + "."));
 DelayCommand(1.2, Hurt(pc));
 DelayCommand(1.5, AssignCommand(n, ActionSpeakStringByStrRef(0)));
}
void Fight(object pc, object n) { AssignCommand(n, ActionAttack(pc)); }
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Calm(object n) { AssignCommand(n, ClearAllActions(TRUE)); }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(11.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
 ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); SetPlotFlag(n, TRUE); SetPlotFlag(pc, TRUE);
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 55.0));
 DelayCommand(3.0, Lines(pc, n, "one"));
 DelayCommand(5.5, Log("rightclick|0.281,0.822"));
 DelayCommand(6.3, VShot("a_menu"));
 DelayCommand(6.6, Log("hover|0.2797,0.6431"));
 DelayCommand(7.4, Log("click|0.2797,0.6431|0.3"));
 DelayCommand(8.0, Log("hover|off"));
 DelayCommand(8.5, VShot("b_after_top"));
 DelayCommand(9.0, Lines(pc, n, "two"));
 DelayCommand(10.0, Fight(pc, n));
 DelayCommand(15.0, Calm(n));
 DelayCommand(16.0, VShot("c_after_top_lines"));
 // The same menu again: is the top option now "Combine"?
 DelayCommand(17.0, Log("rightclick|0.281,0.822"));
 DelayCommand(17.8, VShot("d_menu_again"));
 DelayCommand(18.1, Log("hover|0.2797,0.6431"));
 DelayCommand(18.9, Log("click|0.2797,0.6431|0.3"));
 DelayCommand(19.5, Log("hover|off"));
 DelayCommand(20.0, VShot("e_after_top_again"));
 DelayCommand(21.0, Lines(pc, n, "three"));
 DelayCommand(24.0, VShot("f_lines_again"));
 DelayCommand(25.0, VDone());
}
