#include "visual_inc"
// What the chat window shows and how: a conversation's lines and answers, talk, a shout, a server message, damage
// feedback and a line the player types, in a window dragged taller so they all show.
void Talk(object o, string s, int vol) { AssignCommand(o, SpeakString(s, vol)); }
void Hurt(object pc) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), pc); }
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(11.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
 ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); SetPlotFlag(n, TRUE);
 SetLocalString(GetModule(), "case", "chatlines");
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 55.0));
 DelayCommand(2.5, Log("hover|0.281,0.822"));
 DelayCommand(3.0, Log("mouse|0.281,0.822|down"));
 DelayCommand(3.2, Log("hover|0.281,0.5"));
 DelayCommand(4.0, Log("mouse|0.281,0.5|up"));
 DelayCommand(4.3, Log("hover|off"));
 DelayCommand(5.0, AssignCommand(n, ActionStartConversation(pc, "ce_talk", FALSE, FALSE)));
 DelayCommand(7.0, Log("key|49|tap"));
 DelayCommand(8.5, Log("key|49|tap"));
 DelayCommand(10.0, Talk(n, "Talk line from the probe.", TALKVOLUME_TALK));
 DelayCommand(10.3, Talk(pc, "Talk line from the player.", TALKVOLUME_TALK));
 DelayCommand(10.6, Talk(n, "Shout from the probe.", TALKVOLUME_SHOUT));
 DelayCommand(10.9, SendMessageToPC(pc, "Server message."));
 DelayCommand(11.2, Hurt(pc));
 DelayCommand(12.0, Log("key|13|tap"));
 DelayCommand(12.6, Log("text|Typed line from the player."));
 DelayCommand(14.0, Log("key|13|tap"));
 DelayCommand(16.0, VShot("a_chat"));
 // A short fight (attack and damage lines), then the chat menu's top option: Split Chat Window (on the original
 // a posted click doesn't reach its menu, see parity/README; this half checks OpenWinter's split).
 DelayCommand(16.5, SetPlotFlag(pc, TRUE));
 DelayCommand(17.0, AssignCommand(n, ActionAttack(pc)));
 DelayCommand(22.0, AssignCommand(n, ClearAllActions(TRUE)));
 DelayCommand(23.0, Log("rightclick|0.281,0.508"));
 DelayCommand(24.0, Log("click|0.281,0.369"));
 DelayCommand(25.5, VShot("b_split"));
 DelayCommand(26.5, VDone());
}
