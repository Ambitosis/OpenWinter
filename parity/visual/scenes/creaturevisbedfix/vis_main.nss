// The previous (6,7) probe snapped to (6.9,7). Explicitly validate that actual point.
#include "visibility_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    object bim = GetObjectByTag("M1Q0ABim");
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc,1.0,89.0,1.0,60.0);
    CVQuiet(bim);
    CVQuiet(GetObjectByTag("M1Q0APavel"));
    DelayCommand(2.0,VCamera(pc,90.0,35.0,5.0));
    DelayCommand(3.0,CVReport(pc,bim,"a_near",Vector(22.0,16.0,0.0)));
    CVTrial(pc,bim,5.0,"b_inside",Vector(6.9,7.0,0.0),35.0);
    DelayCommand(14.0,CVKey("up"));
    DelayCommand(14.0,VMovePC(pc,22.0,16.0,0.0,90.0));
    DelayCommand(15.0,CVReport(pc,bim,"c_return_early",Vector(22.0,16.0,0.0)));
    DelayCommand(20.0,CVReport(pc,bim,"c_return_settled",Vector(22.0,16.0,0.0)));
    DelayCommand(22.0,VDone());
}
