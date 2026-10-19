#include "visual_inc"
// A held right button dragged across the screen (docs/ASSOCIATE_COMMANDS.md): press on the summon, drag towards the
// rings' wedges with the button held, and release. OpenWinter's client used to stop here (the harness read the
// button name as a ground offset).
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
location Spot(object pc) { vector v = GetPosition(pc); return Location(GetArea(pc), Vector(v.x + 3.0, v.y, v.z), 180.0); }
void Pin(object o) { if (GetIsObjectValid(o)) { AssignCommand(o, ClearAllActions(TRUE)); AssignCommand(o, JumpToLocation(Spot(GetFirstPC()))); DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), o)); } }
void Summon(object pc) { AssignCommand(pc, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire", VFX_FNF_SUMMON_MONSTER_1), Spot(pc), 600.0)); }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, Summon(pc));
 DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
 DelayCommand(3.0, Pin(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc)));
 // Press on the summon and drag with the button held: the ring must open and follow the pointer.
 DelayCommand(6.0, Log("mouse|0.734,0.600|down|right"));
 DelayCommand(6.6, Log("hover|0.760,0.600"));
 DelayCommand(7.0, Log("hover|0.800,0.600"));
 DelayCommand(7.4, Log("hover|0.830,0.600"));
 DelayCommand(8.5, VShot("a_drag_east"));
 DelayCommand(9.0, Log("hover|0.734,0.600"));
 DelayCommand(9.5, Log("mouse|0.734,0.600|up|right"));
 DelayCommand(10.5, VShot("b_released_centre"));
 DelayCommand(11.0, Log("key|27|tap"));
 // Again on the ground, released over a wedge.
 DelayCommand(13.0, Log("mouse|0.450,0.650|down|right"));
 DelayCommand(13.5, Log("hover|0.470,0.700"));
 DelayCommand(14.0, Log("hover|0.500,0.750"));
 DelayCommand(15.0, VShot("c_ground_drag"));
 DelayCommand(15.5, Log("mouse|0.500,0.750|up|right"));
 DelayCommand(16.5, VShot("d_ground_released"));
 DelayCommand(17.0, Log("key|27|tap"));
 DelayCommand(18.0, Log("hover|0.300,0.300"));
 DelayCommand(19.0, VShot("e_after"));
 DelayCommand(20.0, VDone());
}
