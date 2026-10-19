#include "visual_inc"
#include "cv_inc"
// Follow-up to convobusy2: the player's own attack or spell while a conversation is open, and a creature's
// BeginConversation with a player in combat (ActionStartConversation got "You are too excited to talk right now.").
object Dummy() { return GetObjectByTag("probe_dummy"); }
void Strip(object o) { int ev; for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, ""); SetPlotFlag(o, TRUE); }
// The dummy stands beside the player (1 m in front of it, to the side of the creature), in reach without moving.
void Spawn()
{
    object a = GetArea(GetFirstPC());
    object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(a, Vector(10.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
    ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); Strip(n);
    SetEventScript(n, EVENT_SCRIPT_CREATURE_ON_DIALOGUE, "cv_conv");
    object d = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(a, Vector(8.0, 9.0, 0.0), 270.0), FALSE, "probe_dummy");
    ChangeToStandardFaction(d, STANDARD_FACTION_HOSTILE); Strip(d);
}
void Case(string sName)
{
    object pc = GetFirstPC();
    SetLocalString(GetModule(), "case", sName);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(8.0, 8.0, 0.0), 0.0)));
    Spawn();
    DelayCommand(0.2, Note("begin"));
}
void Clean()
{
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions(TRUE));
    object n = Npc(); SetPlotFlag(n, FALSE); DestroyObject(n);
    object d = Dummy(); SetPlotFlag(d, FALSE); DestroyObject(d);
}
void NpcAsks(string sDlg) { AssignCommand(Npc(), ActionStartConversation(GetFirstPC(), sDlg)); Note("npcAsked"); }
void NpcBegins() { Note("begin=" + IntToString(BeginConversation("cv_talk", GetFirstPC()))); }
void Poll(int k) { Note("t" + IntToString(k)); if (k < 8) DelayCommand(0.75, Poll(k + 1)); }
void Esc() { if (IsInConversation(GetFirstPC())) WriteTimestampedLogEntry("VISUAL|key|27|tap"); }
void PcDoes(int nWhat)
{
    object pc = GetFirstPC();
    if (nWhat == 0) AssignCommand(pc, ActionAttack(Dummy()));
    else AssignCommand(pc, ActionCastSpellAtObject(SPELL_ACID_SPLASH, Dummy(), METAMAGIC_NONE, TRUE));
    Note("pcDoes" + IntToString(nWhat));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 55.0));
    float t = 4.0;
    // 0, 1: an open conversation, then the player attacks the dummy / casts at it.
    DelayCommand(t, Case("open_pc_attacks")); DelayCommand(t + 0.5, NpcAsks("cv_talk")); DelayCommand(t + 2.5, PcDoes(0));
    DelayCommand(t + 2.5, Poll(0)); DelayCommand(t + 9.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    DelayCommand(t, Case("open_pc_casts")); DelayCommand(t + 0.5, NpcAsks("cv_talk")); DelayCommand(t + 2.5, PcDoes(1));
    DelayCommand(t + 2.5, Poll(0)); DelayCommand(t + 9.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    // 2: the dummy attacks the player for 4 s, then a script on the creature calls BeginConversation with the player.
    DelayCommand(t, Case("pc_combat_begin")); DelayCommand(t + 0.5, AssignCommand(Dummy(), ActionAttack(pc)));
    DelayCommand(t + 4.5, AssignCommand(Npc(), NpcBegins())); DelayCommand(t + 4.5, Poll(0)); DelayCommand(t + 9.5, Esc()); DelayCommand(t + 11.0, Clean());
    t += 12.0;
    DelayCommand(t, VDone());
}
