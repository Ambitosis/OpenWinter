#include "visual_inc"
#include "cv_inc"
// Follow-up to convobusy: which of the talker's own actions end a conversation already open (an attack ended it
// there), the player in combat when a creature starts one, a fight under the stock AI, and when the busy refusal
// comes for a player asking from afar.
object Dummy() { return GetObjectByTag("probe_dummy"); }
void Strip(object o) { int ev; for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, ""); SetPlotFlag(o, TRUE); }
// The dummy stands beside the creature (1 m behind it), in reach without moving.
void Spawn(int bDummy, int bStock = FALSE)
{
    object a = GetArea(GetFirstPC());
    object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(a, Vector(10.0, 8.0, 0.0), 180.0), FALSE, "probe_npc");
    ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); SetPlotFlag(n, TRUE);
    if (!bStock) Strip(n);
    SetEventScript(n, EVENT_SCRIPT_CREATURE_ON_DIALOGUE, "cv_conv");
    if (!bDummy) return;
    object d = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(a, Vector(11.0, 8.0, 0.0), 180.0), FALSE, "probe_dummy");
    ChangeToStandardFaction(d, STANDARD_FACTION_HOSTILE); Strip(d);
}
void Case(string sName, int bDummy, int bStock = FALSE, float fX = 8.0)
{
    object pc = GetFirstPC();
    SetLocalString(GetModule(), "case", sName);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(fX, 8.0, 0.0), 0.0)));
    Spawn(bDummy, bStock);
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
void Fight(object oFrom, object oTo) { AssignCommand(oFrom, ActionAttack(oTo)); Note("fight"); }
void Poll(int k) { Note("t" + IntToString(k)); if (k < 8) DelayCommand(0.75, Poll(k + 1)); }
void Esc() { if (IsInConversation(GetFirstPC())) WriteTimestampedLogEntry("VISUAL|key|27|tap"); }
void Key1() { WriteTimestampedLogEntry("VISUAL|key|49|tap"); }
void NpcDoes(int nWhat)
{
    object n = Npc();
    if (nWhat == 1) AssignCommand(n, ActionMoveToLocation(Location(GetArea(n), Vector(10.0, 11.0, 0.0), 0.0)));
    else if (nWhat == 2) AssignCommand(n, ActionCastSpellAtObject(SPELL_ACID_SPLASH, n, METAMAGIC_NONE, TRUE));
    else if (nWhat == 3) AssignCommand(n, ActionPlayAnimation(ANIMATION_LOOPING_GET_MID, 1.0, 3.0));
    else if (nWhat == 4) AssignCommand(n, ClearAllActions(TRUE));
    else if (nWhat == 5) AssignCommand(n, ActionWait(3.0));
    Note("npcDoes" + IntToString(nWhat));
}
// An open conversation (the creature started it), then at +2 s the creature is given something to do.
float Open(float t, string sName, int nWhat)
{
    DelayCommand(t, Case(sName, nWhat == 0)); DelayCommand(t + 0.5, NpcAsks("cv_talk"));
    if (nWhat == 0) DelayCommand(t + 2.5, Fight(Npc(), Dummy())); else DelayCommand(t + 2.5, NpcDoes(nWhat));
    DelayCommand(t + 2.5, Poll(0)); DelayCommand(t + 9.0, Esc()); DelayCommand(t + 10.0, Clean());
    return t + 11.0;
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 55.0));
    float t = 4.0;
    // 0-5: the creature attacks a dummy in reach / walks 3 m / casts / plays an animation / is cleared / waits.
    t = Open(t, "open_attack_inreach", 0);
    t = Open(t, "open_walk", 1);
    t = Open(t, "open_cast", 2);
    t = Open(t, "open_anim", 3);
    t = Open(t, "open_clear", 4);
    t = Open(t, "open_wait", 5);
    // 6: at the last line (the second, whose one answer ends it), the creature attacks.
    DelayCommand(t, Case("last_attack", TRUE)); DelayCommand(t + 0.5, NpcAsks("cv_talk")); DelayCommand(t + 2.0, Key1());
    DelayCommand(t + 3.5, Fight(Npc(), Dummy())); DelayCommand(t + 3.5, Poll(0)); DelayCommand(t + 9.5, Esc()); DelayCommand(t + 10.5, Clean());
    t += 11.5;
    // 7: the stock AI: the player asks a creature with its standard scripts, then the dummy attacks it.
    DelayCommand(t, Case("stock_attacked", TRUE, TRUE)); DelayCommand(t + 0.5, PcAsks()); DelayCommand(t + 2.5, Fight(Dummy(), Npc()));
    DelayCommand(t + 2.5, Poll(0)); DelayCommand(t + 9.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    // 8: the player has been attacked for 4 s (in combat), then the creature starts a conversation.
    DelayCommand(t, Case("pc_combat_npc_asks", TRUE)); DelayCommand(t + 0.5, Fight(Dummy(), pc)); DelayCommand(t + 4.5, NpcAsks("cv_talk"));
    DelayCommand(t + 4.5, Poll(0)); DelayCommand(t + 9.5, Esc()); DelayCommand(t + 11.0, Clean());
    t += 12.0;
    // 9: the player asks a fighting creature from 9 m away (does the refusal come at once or on arrival?).
    DelayCommand(t, Case("far_npc_attacking", TRUE, FALSE, 1.0)); DelayCommand(t + 0.5, Fight(Npc(), Dummy())); DelayCommand(t + 3.0, PcAsks());
    DelayCommand(t + 3.0, Poll(0)); DelayCommand(t + 6.0, VShot("far_npc_attacking")); DelayCommand(t + 9.0, Esc()); DelayCommand(t + 10.0, Clean());
    t += 11.0;
    DelayCommand(t, VDone());
}
