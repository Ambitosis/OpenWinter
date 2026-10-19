#include "visual_inc"
// The chat window's kinds of line (talk, shout, server messages, combat feedback), then a right click on its bar,
// which the original uses to split chat and combat into two windows.
void Talk(object o, string s, int vol) { AssignCommand(o, SpeakString(s, vol)); }
void Hurt(object pc) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), pc); }
void Msg(object pc, string s) { SendMessageToPC(pc, s); }
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Lines(object pc, object n, string k) {
 Talk(n, "Talk line from the probe " + k + ".", TALKVOLUME_TALK);
 DelayCommand(0.3, Talk(pc, "Talk line from the player " + k + ".", TALKVOLUME_TALK));
 DelayCommand(0.6, Talk(n, "Shout from the probe " + k + ".", TALKVOLUME_SHOUT));
 DelayCommand(0.9, Msg(pc, "Server message " + k + "."));
 DelayCommand(1.2, Hurt(pc));
}
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(11.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
 ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); SetPlotFlag(n, TRUE);
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 55.0));
 DelayCommand(3.0, Lines(pc, n, "one"));
 // Drag the bar up to show more lines (hover keeps a posted, held pointer moving).
 DelayCommand(5.0, Log("hover|0.281,0.822"));
 DelayCommand(5.5, Log("mouse|0.281,0.822|down"));
 DelayCommand(5.7, Log("hover|0.281,0.6"));
 DelayCommand(6.5, Log("mouse|0.281,0.6|up"));
 DelayCommand(7.5, VShot("a_lines_tall"));
 // Right click the bar (now higher up), pointer resting on it first.
 DelayCommand(8.0, Log("hover|0.281,0.6"));
 DelayCommand(8.5, Log("rightclick|0.281,0.6|0.2"));
 DelayCommand(10.0, VShot("b_rightclicked"));
 DelayCommand(11.0, Log("hover|off"));
 DelayCommand(11.0, Lines(pc, n, "two"));
 DelayCommand(14.0, VShot("c_more_lines"));
 DelayCommand(15.0, Log("hover|0.281,0.6"));
 DelayCommand(15.5, Log("rightclick|0.281,0.6|0.2"));
 DelayCommand(17.0, VShot("d_rightclicked_again"));
 DelayCommand(17.5, Log("hover|off"));
 DelayCommand(18.0, Lines(pc, n, "three"));
 DelayCommand(21.0, VShot("e_lines_again"));
 DelayCommand(22.0, VDone());
}
