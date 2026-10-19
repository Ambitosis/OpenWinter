#include "visual_inc"
// OpenWinter-only check of the associate radial (docs/ASSOCIATE_COMMANDS.md): OpenWinter's harness pointer takes
// posted right-clicks, so each ring opens with one and its NW and W rings are entered with numpad keys. Run with
// --only openwinter; the original's rings are in associateradial / associatesubmenus / associatedominated.
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
location Spot(object pc) { vector v = GetPosition(pc); return Location(GetArea(pc), Vector(v.x + 3.0, v.y, v.z), 180.0); }
void Pin(object o) { if (GetIsObjectValid(o)) { AssignCommand(o, ClearAllActions(TRUE)); AssignCommand(o, JumpToLocation(Spot(GetFirstPC()))); DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), o)); } }
void Ring(float t, string k, string p) {
 DelayCommand(t, Log("rightclick|" + p));
 DelayCommand(t + 1.0, VShot(k + "_0root"));
 DelayCommand(t + 1.5, Log("key|103|tap"));
 DelayCommand(t + 2.5, VShot(k + "_1nw"));
 DelayCommand(t + 3.0, Log("key|101|tap"));
 DelayCommand(t + 3.5, Log("key|100|tap"));
 DelayCommand(t + 4.5, VShot(k + "_2w"));
 DelayCommand(t + 5.0, Log("key|27|tap"));
}
void Summon(object pc) { AssignCommand(pc, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire", VFX_FNF_SUMMON_MONSTER_1), Spot(pc), 600.0)); }
void Familiar(object pc) { SummonFamiliar(pc); DelayCommand(1.0, Pin(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc))); }
void Hench(object pc) { object h = CreateObject(OBJECT_TYPE_CREATURE, "nw_hen_gal_01", Spot(pc)); AddHenchman(pc, h); DelayCommand(0.5, Pin(h)); }
void Dominate(object pc) { object g = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Spot(pc));
 AssignCommand(pc, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, SupernaturalEffect(EffectDominated()), g, 600.0)); DelayCommand(0.5, Pin(g)); }
void Gone(object a) { if (GetIsObjectValid(a)) { if (GetAssociateType(a) == ASSOCIATE_TYPE_HENCHMAN) RemoveHenchman(GetMaster(a), a); SetPlotFlag(a, FALSE); DestroyObject(a); } }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, Summon(pc));
 DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
 DelayCommand(3.0, Pin(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc)));
 Ring(5.0, "s", "0.734,0.600");
 DelayCommand(11.0, Gone(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc)));
 DelayCommand(12.0, Familiar(pc));
 Ring(15.0, "f", "0.754,0.470");
 DelayCommand(21.0, Gone(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc)));
 DelayCommand(22.0, Hench(pc));
 Ring(25.0, "h", "0.734,0.540");
 Ring(31.0, "p", "0.975,0.290");                       // the henchman's party portrait
 DelayCommand(37.0, Gone(GetHenchman(pc)));
 DelayCommand(38.0, Dominate(pc));
 Ring(41.0, "d", "0.738,0.555");
 DelayCommand(47.5, VDone());
}
