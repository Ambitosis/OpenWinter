#include "visual_inc"
// Player party control (settings.tml server.player-party-control = true, scene.json "serverSettings"): what a player
// can do with her henchman when the server allows it. SetCommandingPlayer(henchman, player); the henchman's radial
// from the world and from its party portrait (root, NW and W submenus); a left click and a double click on the
// portrait, each followed by W held, with both creatures' positions read; then the override removed and the portrait
// radial again. Needs the real pointer (--real-mouse). Docs: docs/PARTY.md.
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
string Vec(vector v) { return FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2); }
location Spot(object pc) { vector v = GetPosition(pc); return Location(GetArea(pc), Vector(v.x + 3.0, v.y, v.z), 180.0); }
object Hench() { return GetHenchman(GetFirstPC()); }
void Pin(object o) { if (GetIsObjectValid(o)) { AssignCommand(o, ClearAllActions(TRUE)); AssignCommand(o, JumpToLocation(Spot(GetFirstPC()))); DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), o)); } }
void Unpin(object o) { effect e = GetFirstEffect(o); while (GetIsEffectValid(e)) { RemoveEffect(o, e); e = GetNextEffect(o); } }
void Ring(float t, string k, string p) {
 DelayCommand(t, Log("hover|" + p));
 DelayCommand(t + 0.3, Log("mouse|" + p + "|down|right"));
 DelayCommand(t + 1.5, VShot(k + "_0root"));
 DelayCommand(t + 2.0, Log("key|103|tap"));
 DelayCommand(t + 3.0, VShot(k + "_1nw"));
 DelayCommand(t + 3.5, Log("key|101|tap"));
 DelayCommand(t + 4.0, Log("key|100|tap"));
 DelayCommand(t + 5.0, VShot(k + "_2w"));
 DelayCommand(t + 5.5, Log("key|101|tap"));
 DelayCommand(t + 5.8, Log("key|101|tap"));
 DelayCommand(t + 6.1, Log("hover|" + p));
 DelayCommand(t + 6.3, Log("mouse|" + p + "|up|right"));
 DelayCommand(t + 7.0, VShot(k + "_3closed"));
}
void Probe(string k) {
 object pc = GetFirstPC(); object h = Hench();
 Note(k + "|pc=" + Vec(GetPosition(pc)) + "/a" + IntToString(GetCurrentAction(pc)) + "|h=" + Vec(GetPosition(h)) + "/a" + IntToString(GetCurrentAction(h))
  + "|firstPC=" + GetName(GetFirstPC()) + "|hIsPC=" + IntToString(GetIsPC(h)) + "|hPossessed=" + IntToString(GetIsPossessedFamiliar(h))
  + "|hDMPossessed=" + IntToString(GetIsDMPossessed(h)) + "|master=" + IntToString(GetMaster(h) == pc) + "|commandable=" + IntToString(GetCommandable(h))
  + "|leader=" + GetName(GetFactionLeader(h)));
}
void Click(string p) { Log("hover|" + p); DelayCommand(0.2, Log("mouse|" + p + "|down|left")); DelayCommand(0.3, Log("mouse|" + p + "|up|left")); }
void Walk(float t, string k) {
 DelayCommand(t, Probe(k + "_before"));
 DelayCommand(t + 0.2, Log("hover|off"));
 DelayCommand(t + 0.3, Log("key|87|down"));
 DelayCommand(t + 1.8, Log("key|87|up"));
 DelayCommand(t + 2.3, Probe(k + "_after"));
 DelayCommand(t + 2.5, VShot(k + "_walk"));
}
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 SetPlotFlag(pc, TRUE);
 DelayCommand(1.0, VCamera(pc, 90.0, 8.0, 60.0));
 object h = CreateObject(OBJECT_TYPE_CREATURE, "nw_hen_gal_01", Spot(pc), FALSE, "PC_HENCH");
 AddHenchman(pc, h);
 SetPlotFlag(h, TRUE);
 SetCommandingPlayer(h, pc);
 DelayCommand(0.5, Pin(h));
 DelayCommand(2.0, Probe("start"));
 DelayCommand(3.0, VShot("a_world"));
 Ring(4.0, "h", "0.750,0.555");                        // the henchman in the world 4..11
 Ring(12.0, "p", "0.953,0.290");                       // its party portrait 12..19
 DelayCommand(20.0, Unpin(Hench()));
 // A left click on the portrait, then W held.
 DelayCommand(21.0, Probe("click_before"));
 DelayCommand(21.2, Click("0.953,0.290"));
 DelayCommand(22.0, Probe("click_after"));
 DelayCommand(22.2, VShot("c_click"));
 Walk(23.5, "c");
 // A double click on the portrait, then W held.
 DelayCommand(27.5, Click("0.953,0.290"));
 DelayCommand(27.75, Click("0.953,0.290"));
 DelayCommand(28.6, Probe("dbl_after"));
 DelayCommand(28.8, VShot("d_double"));
 Walk(30.0, "d");
 // The override removed: regular party control.
 DelayCommand(34.0, SetCommandingPlayer(Hench(), OBJECT_INVALID));
 DelayCommand(34.5, Pin(Hench()));
 DelayCommand(35.5, Probe("regular"));
 Ring(36.0, "q", "0.953,0.290");                       // the portrait again 36..43
 DelayCommand(44.0, Probe("end"));
 DelayCommand(44.5, VShot("z_end"));
 DelayCommand(45.5, VDone());
}
