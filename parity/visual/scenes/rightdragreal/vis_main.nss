#include "visual_inc"
// OpenWinter-only: the summon to press on with the real right button (tools/visual/realdrag.py drives the pointer
// on the development monitor after a_ready; docs/ASSOCIATE_COMMANDS.md). No harness pointer commands.
location Spot(object pc) { vector v = GetPosition(pc); return Location(GetArea(pc), Vector(v.x + 3.0, v.y, v.z), 180.0); }
void Pin(object o) { if (GetIsObjectValid(o)) { AssignCommand(o, ClearAllActions(TRUE)); AssignCommand(o, JumpToLocation(Spot(GetFirstPC()))); DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), o)); } }
void Summon(object pc) { AssignCommand(pc, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire", VFX_FNF_SUMMON_MONSTER_1), Spot(pc), 600.0)); }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, Summon(pc));
 DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
 DelayCommand(3.0, Pin(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc)));
 DelayCommand(6.0, VShot("a_ready"));
 DelayCommand(10.0, VShot("b_drag"));
 DelayCommand(14.0, VShot("c_after"));
 DelayCommand(16.0, VDone());
}
