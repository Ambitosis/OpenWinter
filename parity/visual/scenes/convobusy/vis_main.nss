#include "visual_inc"
#include "cv_inc"
// When the original refuses a conversation as busy ("The object is busy and can not talk to you right now.",
// strref 6625), and what a fight does to a conversation already open. One case at a time; the chat window is logged.
object Dummy() { return GetObjectByTag("probe_dummy"); }
void Strip(object o) { int ev; for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, ""); SetPlotFlag(o, TRUE); }
void Spawn(int bDummy)
{
    object a = GetArea(GetFirstPC());
    object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(a, Vector(10.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
    ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); Strip(n);
    SetEventScript(n, EVENT_SCRIPT_CREATURE_ON_DIALOGUE, "cv_conv");
    if (!bDummy) return;
    object d = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(a, Vector(9.0, 10.5, 0.0), 270.0), FALSE, "probe_dummy");
    ChangeToStandardFaction(d, STANDARD_FACTION_HOSTILE); Strip(d);
}
void Case(string sName, int bDummy)
{
    object pc = GetFirstPC();
    SetLocalString(GetModule(), "case", sName);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(8.0, 8.0, 0.0), 0.0)));
    Spawn(bDummy);
    DelayCommand(0.2, Note("begin"));
}
void Clean()
{
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions(TRUE));
    object n = Npc(); SetPlotFlag(n, FALSE); DestroyObject(n);
    object d = Dummy(); SetPlotFlag(d, FALSE); DestroyObject(d);
}
void PcAsks(string sDlg = "") { AssignCommand(GetFirstPC(), ActionStartConversation(Npc(), sDlg)); Note("asked"); }
void NpcAsks(string sDlg) { AssignCommand(Npc(), ActionStartConversation(GetFirstPC(), sDlg)); Note("npcAsked"); }
void NpcBegins() { Note("begin=" + IntToString(BeginConversation("cv_talk", GetFirstPC()))); }
void Fight(object oFrom, object oTo) { AssignCommand(oFrom, ActionAttack(oTo)); }
void Kill() { object n = Npc(); SetPlotFlag(n, FALSE); ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), n); }
void Poll(int k) { Note("t" + IntToString(k)); if (k < 8) DelayCommand(0.75, Poll(k + 1)); }
void Esc() { if (IsInConversation(GetFirstPC())) WriteTimestampedLogEntry("VISUAL|key|27|tap"); }

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 55.0));
    float t = 4.0;
    // 0: control, the player asks an idle creature (its OnConversation begins the dialog).
    DelayCommand(t, Case("idle", FALSE)); DelayCommand(t + 1.0, PcAsks()); DelayCommand(t + 1.0, Poll(0));
    DelayCommand(t + 3.0, VShot("idle")); DelayCommand(t + 6.0, Esc()); DelayCommand(t + 9.0, Clean());
    t += 10.0;
    // 1: the creature fights the dummy.
    DelayCommand(t, Case("npc_attacking", TRUE)); DelayCommand(t + 0.5, Fight(Npc(), Dummy())); DelayCommand(t + 3.0, PcAsks());
    DelayCommand(t + 3.0, Poll(0)); DelayCommand(t + 4.5, VShot("npc_attacking")); DelayCommand(t + 8.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    // 2: the dummy attacks the creature, which doesn't fight back.
    DelayCommand(t, Case("npc_attacked", TRUE)); DelayCommand(t + 0.5, Fight(Dummy(), Npc())); DelayCommand(t + 3.0, PcAsks());
    DelayCommand(t + 3.0, Poll(0)); DelayCommand(t + 8.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    // 3: the creature is dead.
    DelayCommand(t, Case("npc_dead", FALSE)); DelayCommand(t + 0.5, Kill()); DelayCommand(t + 2.5, PcAsks());
    DelayCommand(t + 2.5, Poll(0)); DelayCommand(t + 8.0, Esc()); DelayCommand(t + 9.0, Clean());
    t += 10.0;
    // 4: the creature fights; the player names a dialog.
    DelayCommand(t, Case("npc_attacking_named", TRUE)); DelayCommand(t + 0.5, Fight(Npc(), Dummy())); DelayCommand(t + 3.0, PcAsks("cv_talk"));
    DelayCommand(t + 3.0, Poll(0)); DelayCommand(t + 8.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    // 5: the creature is attacked and starts a conversation with the player itself.
    DelayCommand(t, Case("npc_attacked_npc_asks", TRUE)); DelayCommand(t + 0.5, Fight(Dummy(), Npc())); DelayCommand(t + 3.0, NpcAsks("cv_talk"));
    DelayCommand(t + 3.0, Poll(0)); DelayCommand(t + 8.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    // 6: the creature fights and a script on it calls BeginConversation with the player.
    DelayCommand(t, Case("npc_attacking_begin", TRUE)); DelayCommand(t + 0.5, Fight(Npc(), Dummy())); DelayCommand(t + 3.0, AssignCommand(Npc(), NpcBegins()));
    DelayCommand(t + 3.0, Poll(0)); DelayCommand(t + 8.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    // 7: the creature is hostile (not fighting); the player asks.
    DelayCommand(t, Case("npc_hostile", FALSE)); DelayCommand(t + 0.5, ChangeToStandardFaction(Npc(), STANDARD_FACTION_HOSTILE)); DelayCommand(t + 2.0, PcAsks());
    DelayCommand(t + 2.0, Poll(0)); DelayCommand(t + 8.0, Esc()); DelayCommand(t + 9.0, Clean());
    t += 10.0;
    // 8-10: a conversation is open when a fight starts: the dummy attacks the creature / the creature attacks / the
    // dummy attacks the player.
    DelayCommand(t, Case("open_npc_attacked", TRUE)); DelayCommand(t + 0.5, NpcAsks("cv_talk")); DelayCommand(t + 2.5, Fight(Dummy(), Npc()));
    DelayCommand(t + 2.5, Poll(0)); DelayCommand(t + 9.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    DelayCommand(t, Case("open_npc_attacks", TRUE)); DelayCommand(t + 0.5, NpcAsks("cv_talk")); DelayCommand(t + 2.5, Fight(Npc(), Dummy()));
    DelayCommand(t + 2.5, Poll(0)); DelayCommand(t + 9.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    DelayCommand(t, Case("open_pc_attacked", TRUE)); DelayCommand(t + 0.5, NpcAsks("cv_talk")); DelayCommand(t + 2.5, Fight(Dummy(), pc));
    DelayCommand(t + 2.5, Poll(0)); DelayCommand(t + 6.5, VShot("open_pc_attacked")); DelayCommand(t + 9.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    // 11: the player is attacked (in combat) and asks an idle creature.
    DelayCommand(t, Case("pc_attacked", TRUE)); DelayCommand(t + 0.5, Fight(Dummy(), pc)); DelayCommand(t + 3.0, PcAsks());
    DelayCommand(t + 3.0, Poll(0)); DelayCommand(t + 8.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    // 12: the player is attacked and the creature starts a conversation with the player.
    DelayCommand(t, Case("pc_attacked_npc_asks", TRUE)); DelayCommand(t + 0.5, Fight(Dummy(), pc)); DelayCommand(t + 3.0, NpcAsks("cv_talk"));
    DelayCommand(t + 3.0, Poll(0)); DelayCommand(t + 8.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    DelayCommand(t, VDone());
}
