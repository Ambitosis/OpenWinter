// Where the camera aims on different player characters: dogs on the ground 4 m around the player mark the floor,
// seen from SetCameraFacing at 5 m / 50 degrees (a module's opening) and 10 m / 60 degrees. Run it with each
// character (--character) and compare where the dogs and the player fall on screen.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|" + GetName(oPC) + "|pc=" + IntToString(GetIsPC(oPC)));
    VBegin(oPC);
    VSpawn(oPC, "nw_dog", 4.0, 0.0, 180.0);
    VSpawn(oPC, "nw_dog", -4.0, 0.0, 0.0);
    VSpawn(oPC, "nw_dog", 0.0, 4.0, 270.0);
    VSpawn(oPC, "nw_dog", 0.0, -4.0, 90.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 5.0, 50.0));
    DelayCommand(6.0, VShot("d5_p50"));
    DelayCommand(8.0, VCamera(oPC, 90.0, 10.0, 60.0));
    DelayCommand(11.0, VShot("d10_p60"));
    DelayCommand(13.0, VDone());
}
