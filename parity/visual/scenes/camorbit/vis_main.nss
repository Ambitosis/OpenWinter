// Hold and release each orbit key with a stationary target and fixed camera distance.
// Compare the ground (the hidden player's animation cannot affect it) after release.
#include "visual_inc"

void Key(int nKey, string sState)
{
    WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nKey) + "|" + sState);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    SetCutsceneMode(oPC, FALSE);
    VHidePC(oPC);
    DelayCommand(3.0, VCamera(oPC, 90.0, 12.0, 55.0));
    DelayCommand(5.0, VShot("start"));
    DelayCommand(6.0, Key(39, "down"));
    DelayCommand(6.5, VShot("right_held"));
    DelayCommand(7.0, Key(39, "up"));
    DelayCommand(7.1, VShot("right_released"));
    DelayCommand(8.0, VShot("right_settled"));
    DelayCommand(10.0, Key(37, "down"));
    DelayCommand(10.5, VShot("left_held"));
    DelayCommand(11.0, Key(37, "up"));
    DelayCommand(11.1, VShot("left_released"));
    DelayCommand(12.0, VShot("left_settled"));
    DelayCommand(14.0, VDone());
}
