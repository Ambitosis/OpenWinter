#include "visual_inc"
void Hover(string xy) { WriteTimestampedLogEntry("VISUAL|hover|" + xy); }
void Key(string state) { WriteTimestampedLogEntry("VISUAL|key|9|" + state); }
void main() {
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    object c = VSpawn(pc, "nw_bandit001", -2.3, 2.0, 270.0);
    SetName(c, "Creature");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), c);
    object p = VPlaceable(pc, "plc_chest1", 0.0, 2.0);
    SetName(p, "Chest");
    object d = GetObjectByTag("VIS_DOOR");
    SetName(d, "Door");
    DelayCommand(3.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(4.0, Hover("0.5,0.44"));
    DelayCommand(6.0, VShot("a_chest_hover"));
    DelayCommand(8.0, Key("down"));
    DelayCommand(10.0, VShot("b_tab_chest_hover"));
    DelayCommand(12.0, Key("up"));
    DelayCommand(12.0, Hover("0.67,0.36"));
    DelayCommand(14.0, VShot("c_door_hover"));
    DelayCommand(16.0, Hover("0.35,0.35"));
    DelayCommand(18.0, VShot("d_creature_hover"));
    DelayCommand(20.0, Hover("0.95,0.95"));
    DelayCommand(22.0, VShot("e_released"));
    DelayCommand(24.0, VDone());
}
