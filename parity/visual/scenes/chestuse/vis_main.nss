#include "visual_inc"

void Observe(object pc, object chest, int n = 0)
{
    vector v = GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|chest|" + IntToString(n) + "|" + FloatToString(v.x, 0, 3)
        + "," + FloatToString(v.y, 0, 3) + "|" + FloatToString(GetFacing(pc), 0, 1)
        + "|" + IntToString(GetCurrentAction(pc)) + "|" + IntToString(GetIsOpen(chest)));
    if (GetIsOpen(chest) && !GetLocalInt(chest, "shot"))
    {
        SetLocalInt(chest, "shot", TRUE);
        VShot("b_open_00");
        DelayCommand(0.15, VShot("c_open_15"));
        DelayCommand(0.30, VShot("d_open_30"));
        DelayCommand(0.45, VShot("e_open_45"));
        DelayCommand(0.65, VShot("f_open_65"));
        DelayCommand(1.00, VShot("g_open_100"));
        DelayCommand(2.00, VShot("h_open_200"));
    }
    if (n < 65) DelayCommand(0.1, Observe(pc, chest, n + 1));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    object chest = VPlaceable(pc, "plc_chest1", 3.0, 4.0);
    SetTag(chest, "use_chest");
    DelayCommand(3.0, VCamera(pc, 90.0, 9.0, 55.0));
    DelayCommand(5.0, VShot("a_before"));
    // At this camera the chest is on the right. OpenWinter projects the same object by tag.
    DelayCommand(6.0, WriteTimestampedLogEntry("VISUAL|click|0.68,0.26|use_chest"));
    DelayCommand(6.0, Observe(pc, chest));
    DelayCommand(12.0, WriteTimestampedLogEntry("VISUAL|key|27|tap"));
    DelayCommand(13.0, VShot("i_closed"));
    DelayCommand(15.0, VDone());
}
