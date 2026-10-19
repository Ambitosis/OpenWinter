#include "visual_inc"
// Follow-up to associatesubmenus: the dominated creature's NW submenu, and the henchman's radial opened in the world.
// Docs: docs/ASSOCIATE_COMMANDS.md.
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
location Spot(object pc) { vector v = GetPosition(pc); return Location(GetArea(pc), Vector(v.x + 3.0, v.y, v.z), 180.0); }
void Pin(object o) { if (GetIsObjectValid(o)) { AssignCommand(o, ClearAllActions(TRUE)); AssignCommand(o, JumpToLocation(Spot(GetFirstPC()))); DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), o)); } }
void Ring(float t, string k, string p) {
 DelayCommand(t, Log("hover|" + p));
 DelayCommand(t + 0.3, Log("mouse|" + p + "|down|right"));
 DelayCommand(t + 1.5, VShot(k + "_0root"));
 DelayCommand(t + 2.0, Log("key|103|tap"));
 DelayCommand(t + 3.0, VShot(k + "_1nw"));
 DelayCommand(t + 3.5, Log("key|101|tap"));
 DelayCommand(t + 3.8, Log("key|101|tap"));
 DelayCommand(t + 4.1, Log("hover|" + p));
 DelayCommand(t + 4.3, Log("mouse|" + p + "|up|right"));
 DelayCommand(t + 5.0, VShot(k + "_3closed"));
}
void Hench(object pc) { object h = CreateObject(OBJECT_TYPE_CREATURE, "nw_hen_gal_01", Spot(pc)); AddHenchman(pc, h); DelayCommand(0.5, Pin(h)); }
void Dominate(object pc) { object g = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Spot(pc));
 AssignCommand(pc, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, SupernaturalEffect(EffectDominated()), g, 600.0)); DelayCommand(0.5, Pin(g)); }
void Gone(object a) { if (GetIsObjectValid(a)) { if (GetAssociateType(a) == ASSOCIATE_TYPE_HENCHMAN) RemoveHenchman(GetMaster(a), a); SetPlotFlag(a, FALSE); DestroyObject(a); } }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, Dominate(pc));
 DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
 Ring(4.0, "d", "0.738,0.555");                       // dominated 4..9
 DelayCommand(10.0, Gone(GetAssociate(ASSOCIATE_TYPE_DOMINATED, pc)));
 DelayCommand(11.0, Hench(pc));
 Ring(14.0, "h", "0.734,0.540");                      // henchman 14..19
 DelayCommand(20.5, VShot("z_end"));
 DelayCommand(21.5, VDone());
}
