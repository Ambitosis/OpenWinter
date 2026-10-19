#include "visual_inc"

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(3.0, VCamera(pc, 0.0, 5.0, 65.0));
    DelayCommand(4.0, WriteTimestampedLogEntry("VISUAL|hover|0.5,0.32"));
    DelayCommand(5.0, VShot("a_wall_aim"));
    DelayCommand(6.0, WriteTimestampedLogEntry("VISUAL|click|0.5,0.32"));
    DelayCommand(6.1, VShot("b_wall_order"));
    DelayCommand(10.0, VShot("c_after_order"));
    DelayCommand(12.0, VDone());
}
