#include "visual_inc"
void State(string label)
{
    vector p = GetPosition(GetFirstPC());
    WriteTimestampedLogEntry("VISUAL|note|door|" + label + "|plain=" + IntToString(GetIsOpen(GetObjectByTag("plain")))
        + "|linked=" + IntToString(GetIsOpen(GetObjectByTag("linked"))) + "|target=" + GetTag(GetTransitionTarget(GetObjectByTag("linked")))
        + "|pc=" + FloatToString(p.x) + "," + FloatToString(p.y));
}
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    object plain = GetObjectByTag("plain");
    object linked = GetObjectByTag("linked");
    SetLocked(plain, FALSE); SetLocked(linked, FALSE);
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(4.0, VShot("a_closed"));
    DelayCommand(6.0, AssignCommand(plain, ActionOpenDoor(plain)));
    DelayCommand(6.0, AssignCommand(linked, ActionOpenDoor(linked)));
    DelayCommand(9.0, VShot("b_open"));
    DelayCommand(10.0, State("open"));
    DelayCommand(11.0, WriteTimestampedLogEntry("VISUAL|key|9|down"));
    DelayCommand(13.0, VShot("c_highlight"));
    DelayCommand(14.0, WriteTimestampedLogEntry("VISUAL|key|9|up"));
    DelayCommand(15.0, WriteTimestampedLogEntry("VISUAL|hover|0.645,0.32"));
    DelayCommand(17.0, VShot("d_hover_link"));
    DelayCommand(18.0, WriteTimestampedLogEntry("VISUAL|mouse|0.645,0.32|down|right"));
    DelayCommand(20.0, VShot("e_radial_link"));
    DelayCommand(20.5, WriteTimestampedLogEntry("VISUAL|mouse|0.645,0.32|up|right"));
    DelayCommand(20.7, WriteTimestampedLogEntry("VISUAL|key|101|tap"));
    DelayCommand(21.0, WriteTimestampedLogEntry("VISUAL|mouse|0.593,0.28|down|right"));
    DelayCommand(23.0, VShot("f_radial_second"));
    DelayCommand(24.0, WriteTimestampedLogEntry("VISUAL|key|102|tap"));
    DelayCommand(25.5, WriteTimestampedLogEntry("VISUAL|mouse|0.593,0.28|up|right"));
    DelayCommand(27.0, State("radial_close"));
    DelayCommand(28.0, VShot("g_after_close"));
    DelayCommand(29.0, WriteTimestampedLogEntry("VISUAL|key|101|tap"));
    DelayCommand(30.0, AssignCommand(linked, ActionOpenDoor(linked)));
    DelayCommand(31.0, VMovePC(pc, 20.0, 15.0, 0.0, 90.0));
    DelayCommand(32.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(33.0, WriteTimestampedLogEntry("VISUAL|click|0.645,0.32|0.2"));
    DelayCommand(36.0, State("left_transition"));
    DelayCommand(37.0, VShot("h_after_left"));
    DelayCommand(38.0, VMovePC(pc, 20.0, 15.0, 0.0, 90.0));
    DelayCommand(39.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(40.0, WriteTimestampedLogEntry("VISUAL|click|0.385,0.32|0.2"));
    DelayCommand(43.0, State("plain_opening_click"));
    DelayCommand(43.5, VShot("i_plain_opening"));
    DelayCommand(44.0, VMovePC(pc, 20.0, 15.0, 0.0, 90.0));
    DelayCommand(45.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(47.0, WriteTimestampedLogEntry("VISUAL|click|0.34,0.30|0.2"));
    DelayCommand(51.0, State("plain_leaf_click"));
    DelayCommand(52.0, VShot("j_plain_leaf"));
    DelayCommand(54.0, VDone());
}
