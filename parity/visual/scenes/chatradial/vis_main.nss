#include "visual_inc"
// The chat window's right-click menu: each option's label (hovered), then the top option chosen.
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Talk(object o, string s, int vol) { AssignCommand(o, SpeakString(s, vol)); }
void Hurt(object pc) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), pc); }
void Lines(object pc, object n, string k) {
 Talk(n, "Talk line from the probe " + k + ".", TALKVOLUME_TALK);
 DelayCommand(0.3, SendMessageToPC(pc, "Server message " + k + "."));
 DelayCommand(0.6, Hurt(pc));
}
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(11.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
 ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); SetPlotFlag(n, TRUE);
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 55.0));
 DelayCommand(3.0, Lines(pc,n,"one"));
 DelayCommand(5.0, Log("rightclick|0.281,0.822"));
 DelayCommand(5.8, Log("hover|0.2797,0.6431"));
 DelayCommand(7.0, VShot("label_top"));
 DelayCommand(7.4, Log("hover|off"));
 DelayCommand(7.5, Log("rightclick|0.85,0.25"));
 DelayCommand(7.9, Log("rightclick|0.85,0.25"));
 DelayCommand(9.0, Log("rightclick|0.281,0.822"));
 DelayCommand(9.8, Log("hover|0.3359,0.6833"));
 DelayCommand(11.0, VShot("label_upright"));
 DelayCommand(11.4, Log("hover|off"));
 DelayCommand(11.5, Log("rightclick|0.85,0.25"));
 DelayCommand(11.9, Log("rightclick|0.85,0.25"));
 DelayCommand(13.0, Log("rightclick|0.281,0.822"));
 DelayCommand(13.8, Log("hover|0.3578,0.7819"));
 DelayCommand(15.0, VShot("label_right"));
 DelayCommand(15.4, Log("hover|off"));
 DelayCommand(15.5, Log("rightclick|0.85,0.25"));
 DelayCommand(15.9, Log("rightclick|0.85,0.25"));
 DelayCommand(17.0, Log("rightclick|0.281,0.822"));
 DelayCommand(17.8, Log("hover|0.3359,0.8819"));
 DelayCommand(19.0, VShot("label_downright"));
 DelayCommand(19.4, Log("hover|off"));
 DelayCommand(19.5, Log("rightclick|0.85,0.25"));
 DelayCommand(19.9, Log("rightclick|0.85,0.25"));
 DelayCommand(21.0, Log("rightclick|0.281,0.822"));
 DelayCommand(21.8, Log("hover|0.2797,0.9208"));
 DelayCommand(23.0, VShot("label_bottom"));
 DelayCommand(23.4, Log("hover|off"));
 DelayCommand(23.5, Log("rightclick|0.85,0.25"));
 DelayCommand(23.9, Log("rightclick|0.85,0.25"));
 DelayCommand(25.0, Log("rightclick|0.281,0.822"));
 DelayCommand(25.8, Log("hover|0.2250,0.8819"));
 DelayCommand(27.0, VShot("label_downleft"));
 DelayCommand(27.4, Log("hover|off"));
 DelayCommand(27.5, Log("rightclick|0.85,0.25"));
 DelayCommand(27.9, Log("rightclick|0.85,0.25"));
 DelayCommand(29.0, Log("rightclick|0.281,0.822"));
 DelayCommand(29.8, Log("hover|0.2016,0.7819"));
 DelayCommand(31.0, VShot("label_left"));
 DelayCommand(31.4, Log("hover|off"));
 DelayCommand(31.5, Log("rightclick|0.85,0.25"));
 DelayCommand(31.9, Log("rightclick|0.85,0.25"));
 DelayCommand(33.0, Log("rightclick|0.281,0.822"));
 DelayCommand(33.8, Log("hover|0.2797,0.7847"));
 DelayCommand(35.0, VShot("label_centre"));
 DelayCommand(35.4, Log("hover|off"));
 DelayCommand(35.5, Log("rightclick|0.85,0.25"));
 DelayCommand(35.9, Log("rightclick|0.85,0.25"));
 DelayCommand(37.0, Log("rightclick|0.281,0.822"));
 DelayCommand(37.8, VShot("menu_open"));
 DelayCommand(38.2, Log("click|0.2797,0.6431"));
 DelayCommand(39.5, VShot("after_top"));
 DelayCommand(40.0, Lines(pc,n,"two"));
 DelayCommand(42.5, VShot("after_top_lines"));
 DelayCommand(44.0, VDone());
}
