// Open ground controls. Distances are probe points, not proposed render thresholds.
#include "visibility_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc,1.0,89.0,1.0,60.0);
    SetPlotFlag(pc,TRUE);
    object actor = VSpawn(pc,"cv_actor",5.0,0.0,270.0);
    SetTag(actor,"CV_ACTOR");
    CVQuiet(actor);
    // A placeable beside the target is a framing control, not proof of NPC visibility.
    object marker = VPlaceable(pc,"plc_chest1",5.0,2.0,0.0);
    SetTag(marker,"CV_MARKER");
    WriteTimestampedLogEntry("VISUAL|note|setup|actor=" + IntToString(GetIsObjectValid(actor))
        + "|marker=" + IntToString(GetIsObjectValid(marker)));
    CVTrial(pc,actor,2.0,"a_near5",Vector(40.0,40.0,0.0),50.0);
    CVTrial(pc,actor,12.0,"b_out19",Vector(26.0,40.0,0.0),50.0);
    CVTrial(pc,actor,22.0,"c_out21",Vector(24.0,40.0,0.0),50.0);
    CVTrial(pc,actor,32.0,"d_out35",Vector(10.0,40.0,0.0),50.0);
    CVTrial(pc,actor,42.0,"e_return19",Vector(26.0,40.0,0.0),50.0);
    DelayCommand(51.0,CVKey("up"));
    DelayCommand(53.0,VDone());
}
