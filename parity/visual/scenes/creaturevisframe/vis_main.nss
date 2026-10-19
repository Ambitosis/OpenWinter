#include "visibility_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    object bim = GetObjectByTag("M1Q0ABim");
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc,1.0,89.0,1.0,60.0);
    CVQuiet(bim);
    DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(4.0,CVReport(pc,bim,"a_known_near",Vector(22.0,16.0,0.0)));
    DelayCommand(6.0,VCamera(pc,90.0,35.0,5.0));
    DelayCommand(8.0,CVReport(pc,bim,"b_wide_near",Vector(22.0,16.0,0.0)));
    DelayCommand(10.0,VDone());
}
