#include "visual_inc"
// The chat window's menu driven with a held right button (posted held drags move the original's pointer):
// each option's label while hovered, a dwell on the bottom option, then release over the top option (split).
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Talk(object o, string s, int vol) { AssignCommand(o, SpeakString(s, vol)); }
void Hurt(object pc) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), pc); }
void Save(object pc) { FortitudeSave(pc, 12); }
void Skill(object pc) { GetIsSkillSuccessful(pc, SKILL_LISTEN, 15); }
void Lines(object pc, object n, string k) {
 Talk(n, "Talk line from the probe " + k + ".", TALKVOLUME_TALK);
 DelayCommand(0.3, Talk(n, "Shout from the probe " + k + ".", TALKVOLUME_SHOUT));
 DelayCommand(0.6, SendMessageToPC(pc, "Server message " + k + "."));
 DelayCommand(0.9, Hurt(pc));
 DelayCommand(1.2, Save(pc));
 DelayCommand(1.5, Skill(pc));
}
void Fight(object pc, object n) { AssignCommand(n, ActionAttack(pc)); }
void Calm(object n) { AssignCommand(n, ClearAllActions(TRUE)); }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(11.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
 ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); SetPlotFlag(n, TRUE); SetPlotFlag(pc, TRUE);
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 55.0));
 DelayCommand(3.0, Lines(pc, n, "one"));
 DelayCommand(5.0, Log("mouse|0.85,0.25|down|right"));
 DelayCommand(5.5, Log("hover|0.281,0.822"));
 DelayCommand(6.1, Log("mouse|0.281,0.822|up|right"));
 DelayCommand(7.0, Log("mouse|0.281,0.822|down|right"));
 DelayCommand(7.8, VShot("m_open"));
 DelayCommand(8.2, Log("hover|0.2797,0.6431"));
 DelayCommand(9.1, VShot("m_top"));
 DelayCommand(9.5, Log("hover|0.3359,0.6833"));
 DelayCommand(10.4, VShot("m_upright"));
 DelayCommand(10.8, Log("hover|0.3578,0.7819"));
 DelayCommand(11.7, VShot("m_right"));
 DelayCommand(12.1, Log("hover|0.3359,0.8819"));
 DelayCommand(13.0, VShot("m_downright"));
 DelayCommand(13.4, Log("hover|0.2797,0.9208"));
 DelayCommand(14.3, VShot("m_bottom"));
 DelayCommand(14.7, Log("hover|0.225,0.8819"));
 DelayCommand(15.6, VShot("m_downleft"));
 DelayCommand(16.0, Log("hover|0.2016,0.7819"));
 DelayCommand(16.9, VShot("m_left"));
 DelayCommand(17.3, Log("hover|0.225,0.6833"));
 DelayCommand(18.2, VShot("m_upleft"));
 DelayCommand(18.6, Log("hover|0.2797,0.6431"));
 DelayCommand(19.2, Log("mouse|0.2797,0.6431|up|right"));
 DelayCommand(19.6, Log("hover|off"));
 DelayCommand(20.6, VShot("s_split"));
 DelayCommand(21.0, Lines(pc, n, "two"));
 DelayCommand(24.0, VShot("s_lines"));
 DelayCommand(24.4, Log("mouse|0.85,0.25|down|right"));
 DelayCommand(24.9, Log("hover|0.4219,0.822"));
 DelayCommand(25.5, Log("mouse|0.4219,0.822|up|right"));
 DelayCommand(26.4, Log("mouse|0.4219,0.55|down"));
 DelayCommand(26.8, Log("hover|0.4219,0.55"));
 DelayCommand(27.4, Log("mouse|0.4219,0.55|up"));
 DelayCommand(27.7, Log("hover|off"));
 DelayCommand(28.5, VShot("r_right_taller"));
 DelayCommand(28.9, Log("mouse|0.85,0.25|down|right"));
 DelayCommand(29.4, Log("hover|0.1406,0.822"));
 DelayCommand(30.0, Log("mouse|0.1406,0.822|up|right"));
 DelayCommand(30.9, Log("mouse|0.1406,0.70|down"));
 DelayCommand(31.3, Log("hover|0.1406,0.70"));
 DelayCommand(31.9, Log("mouse|0.1406,0.70|up"));
 DelayCommand(32.2, Log("hover|off"));
 DelayCommand(33.0, VShot("r_left_taller"));
 DelayCommand(33.4, Lines(pc, n, "three"));
 DelayCommand(35.9, Fight(pc, n));
 DelayCommand(40.9, Calm(n));
 DelayCommand(41.9, VShot("r_lines"));
 DelayCommand(42.3, Log("mouse|0.85,0.25|down|right"));
 DelayCommand(42.8, Log("hover|0.1406,0.7"));
 DelayCommand(43.4, Log("mouse|0.1406,0.7|up|right"));
 DelayCommand(44.3, Log("mouse|0.1406,0.70|down|right"));
 DelayCommand(45.1, VShot("c_menu_split"));
 DelayCommand(45.5, Log("mouse|0.1406,0.70|up|right"));
 DelayCommand(46.3, VDone());
}
