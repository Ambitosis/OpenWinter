#include "visual_inc"
void Key(string state) { WriteTimestampedLogEntry("VISUAL|key|9|" + state); }
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    VHidePC(pc);
    object a = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_armoire", Location(GetArea(pc), Vector(20.0, 21.0, 0.0), 180.0));
    SetUseableFlag(a, TRUE);
    DelayCommand(1.0, VCamera(pc, 270.0, 6.0, 65.0));
    DelayCommand(3.0, VShot("a_plain"));
    DelayCommand(5.0, Key("down"));
    DelayCommand(7.0, VShot("b_tab"));
    DelayCommand(9.0, Key("up"));
    DelayCommand(11.0, VShot("c_released"));
    DelayCommand(13.0, AssignCommand(a, PlayAnimation(ANIMATION_PLACEABLE_OPEN)));
    DelayCommand(16.0, Key("down"));
    DelayCommand(18.0, VShot("d_open_tab"));
    DelayCommand(20.0, Key("up"));
    DelayCommand(22.0, VShot("e_open_released"));
    DelayCommand(24.0, VCamera(pc, 180.0, 6.0, 65.0));
    DelayCommand(26.0, VShot("f_front_plain"));
    DelayCommand(28.0, Key("down"));
    DelayCommand(30.0, VShot("g_front_tab"));
    DelayCommand(32.0, Key("up"));
    DelayCommand(34.0, VDone());
}
