#include "visual_inc"

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(3.0, VCamera(pc, 0.0, 12.0, 0.0));
    DelayCommand(5.0, VShot("a_script_overhead"));
    DelayCommand(6.0, WriteTimestampedLogEntry("VISUAL|hover|0.68,0.25"));
    DelayCommand(7.0, WriteTimestampedLogEntry("VISUAL|click|0.68,0.25"));
    DelayCommand(7.1, VShot("b_cap_order"));
    DelayCommand(11.0, VShot("c_after_order"));
    DelayCommand(13.0, VMovePC(pc, 3.0, 2.5, 0.0, 0.0));
    DelayCommand(15.0, VCamera(pc, 0.0, 12.0, 40.0));
    DelayCommand(17.0, WriteTimestampedLogEntry("VISUAL|key|45|tap"));
    DelayCommand(19.0, VShot("d_insert_limit"));
    DelayCommand(21.0, VCamera(pc, 0.0, 12.0, 0.0));
    DelayCommand(23.0, WriteTimestampedLogEntry("VISUAL|hover|0.5,0.4"));
    DelayCommand(24.0, WriteTimestampedLogEntry("VISUAL|click|0.5,0.4"));
    DelayCommand(24.1, VShot("e_valid_floor"));
    DelayCommand(26.0, VMovePC(pc, 3.0, 2.5, 0.0, 0.0));
    // Identical world destination: background SDL clicks need not use the posted screen coordinate.
    DelayCommand(28.0, AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(8.0, -0.5, 0.0), 0.0))));
    DelayCommand(29.0, VShot("f_world_order_1s"));
    DelayCommand(32.0, VShot("g_world_order_4s"));
    DelayCommand(34.0, VDone());
}
