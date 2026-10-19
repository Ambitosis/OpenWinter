#include "visual_inc"
// A door whose transition target never exists: what clicking it does, closed and open. The original takes posted
// clicks at the real pointer's resting place (about 365,575 here), so the door stands where that lands.
void State(string label)
{
    vector p = GetPosition(GetFirstPC());
    object l = GetObjectByTag("linked");
    WriteTimestampedLogEntry("VISUAL|note|door|" + label + "|plain=" + IntToString(GetIsOpen(GetObjectByTag("plain")))
        + "|linked=" + IntToString(GetIsOpen(l)) + "|target=" + IntToString(GetIsObjectValid(GetTransitionTarget(l)))
        + "|pc=" + FloatToString(p.x, 0, 2) + "," + FloatToString(p.y, 0, 2) + "|action=" + IntToString(GetCurrentAction(GetFirstPC())));
}
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Home(object pc) { VMovePC(pc, 20.0, 15.0, 0.0, 90.0); DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 60.0)); }
void Poll(string label, int k) { State(label + "_t" + IntToString(k)); if (k < 8) DelayCommand(0.5, Poll(label, k + 1)); }
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
    SetEventScript(linked, EVENT_SCRIPT_DOOR_ON_CLICKED, "dn_clicked");
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(3.0, State("start"));
    // Closed: hover, then click.
    DelayCommand(4.0, Log("hover|0.645,0.32"));
    DelayCommand(5.5, VShot("a_hover_closed"));
    DelayCommand(6.0, Log("hover|off"));
    DelayCommand(6.5, Log("click|0.645,0.32|0.2"));
    DelayCommand(6.5, Poll("click_closed", 0));
    DelayCommand(11.0, VShot("b_after_click_closed"));
    // Open: hover, then click.
    DelayCommand(12.0, Home(pc));
    DelayCommand(12.5, AssignCommand(linked, ActionOpenDoor(linked)));
    DelayCommand(15.0, Log("hover|0.645,0.32"));
    DelayCommand(16.5, VShot("c_hover_open"));
    DelayCommand(17.0, Log("hover|off"));
    DelayCommand(17.5, Log("click|0.645,0.32|0.2"));
    DelayCommand(17.5, Poll("click_open", 0));
    DelayCommand(22.0, VShot("d_after_click_open"));
    // Open, clicked again from where the first click left the player.
    DelayCommand(23.0, State("before_second"));
    DelayCommand(23.5, AssignCommand(linked, ActionOpenDoor(linked)));
    DelayCommand(25.0, Home(pc));
    DelayCommand(27.0, Log("click|0.645,0.32|0.2"));
    DelayCommand(27.0, Poll("click_open2", 0));
    DelayCommand(32.0, VShot("e_after_click_open2"));
    // Control: the plain door, open, clicked.
    DelayCommand(33.0, Home(pc));
    DelayCommand(33.5, AssignCommand(plain, ActionOpenDoor(plain)));
    DelayCommand(36.0, Log("click|0.385,0.32|0.2"));
    DelayCommand(36.0, Poll("plain_open", 0));
    DelayCommand(41.0, VShot("f_plain_after"));
    DelayCommand(42.0, VDone());
}
