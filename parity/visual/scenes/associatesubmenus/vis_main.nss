#include "visual_inc"
// Follow-up to associateradial: each associate's radial root, its NW and W submenus (entered with numpad keys while
// the right button is held), the henchman's party-portrait radial, and the default left click on the summon.
// Docs: docs/ASSOCIATE_COMMANDS.md.
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
location Spot(object pc) { vector v = GetPosition(pc); return Location(GetArea(pc), Vector(v.x + 3.0, v.y, v.z), 180.0); }
void Pin(object o) { if (GetIsObjectValid(o)) { AssignCommand(o, ClearAllActions(TRUE)); AssignCommand(o, JumpToLocation(Spot(GetFirstPC()))); DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), o)); } }
// The radial at point p (fractions of the window) with the right button held; root, NW submenu, W submenu.
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
void Summon(object pc) { AssignCommand(pc, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire", VFX_FNF_SUMMON_MONSTER_1), Spot(pc), 600.0)); }
void Familiar(object pc) { SummonFamiliar(pc); DelayCommand(1.0, Pin(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc))); }
void Hench(object pc) { object h = CreateObject(OBJECT_TYPE_CREATURE, "nw_hen_gal_01", Spot(pc)); AddHenchman(pc, h); DelayCommand(0.5, Pin(h)); }
void Gone(object a) { if (GetIsObjectValid(a)) { if (GetAssociateType(a) == ASSOCIATE_TYPE_HENCHMAN) RemoveHenchman(GetMaster(a), a); SetPlotFlag(a, FALSE); DestroyObject(a); } }
void Probe(object pc, string k) {
 object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc);
 Note(k + "|pcAction=" + IntToString(GetCurrentAction(pc)) + "|pcTarget=" + GetName(GetAttackTarget(pc)) + "|combat=" + IntToString(GetIsInCombat(pc))
  + "|hp=" + IntToString(GetCurrentHitPoints(s)) + "|lastAttacker=" + GetName(GetLastAttacker(s)) + "|convo=" + IntToString(IsInConversation(pc)) + "|sConvo=" + IntToString(IsInConversation(s))
  + "|dist=" + FloatToString(GetDistanceBetween(pc, s), 0, 2) + "|sAction=" + IntToString(GetCurrentAction(s)));
}
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, Summon(pc));
 DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
 DelayCommand(3.0, Pin(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc)));
 DelayCommand(4.5, VShot("a_world"));
 Ring(5.0, "s", "0.734,0.600");                        // summon 5..12
 // The default action: a held-button left click on the summon.
 DelayCommand(13.0, Probe(pc, "click0"));
 DelayCommand(13.2, Log("hover|0.734,0.600"));
 DelayCommand(13.5, Log("mouse|0.734,0.600|down|left"));
 DelayCommand(13.6, Log("mouse|0.734,0.600|up|left"));
 DelayCommand(13.9, Probe(pc, "click03"));
 DelayCommand(14.0, VShot("s_4click"));
 DelayCommand(14.6, Probe(pc, "click1"));
 DelayCommand(16.0, Probe(pc, "click25"));
 DelayCommand(16.1, VShot("s_5click"));
 DelayCommand(16.5, Log("hover|off"));
 DelayCommand(17.0, AssignCommand(pc, ClearAllActions(TRUE)));
 DelayCommand(17.5, Gone(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc)));
 DelayCommand(18.5, Familiar(pc));
 Ring(21.0, "f", "0.754,0.470");                       // familiar 21..28
 DelayCommand(29.0, Gone(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc)));
 DelayCommand(30.0, Hench(pc));
 Ring(32.0, "h", "0.750,0.555");                       // henchman 32..39
 Ring(41.0, "p", "0.953,0.290");                       // henchman's party portrait 41..48
 DelayCommand(49.5, Gone(GetHenchman(pc)));
 DelayCommand(50.5, VShot("z_end"));
 DelayCommand(51.5, VDone());
}
