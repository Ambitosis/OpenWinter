// Installed Bim and academy geometry, loaded by the fixture at runtime.
#include "visibility_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc,1.0,89.0,1.0,60.0);
    SetPlotFlag(pc,TRUE);
    object bim = GetObjectByTag("M1Q0ABim");
    object pavel = GetObjectByTag("M1Q0APavel");
    if (!GetIsObjectValid(bim))
    {
        WriteTimestampedLogEntry("VISUAL|note|setup|valid=0|missing=Bim");
        VDone();
        return;
    }
    CVQuiet(bim);
    if (GetIsObjectValid(pavel)) CVQuiet(pavel);
    WriteTimestampedLogEntry("VISUAL|note|setup|valid=1|name=" + GetName(bim));
    CVTrial(pc,bim,2.0,"a_bim_near",Vector(22.0,16.0,0.0),35.0);
    CVTrial(pc,bim,12.0,"b_room_edge",Vector(10.0,7.0,0.0),35.0);
    CVTrial(pc,bim,22.0,"c_room_middle",Vector(8.0,7.0,0.0),35.0);
    CVTrial(pc,bim,32.0,"d_room_inside",Vector(6.0,7.0,0.0),35.0);
    CVTrial(pc,bim,42.0,"e_room_return",Vector(10.0,7.0,0.0),35.0);
    CVTrial(pc,bim,52.0,"f_bim_return",Vector(22.0,16.0,0.0),35.0);
    DelayCommand(61.0,CVKey("up"));
    DelayCommand(63.0,VDone());
}
