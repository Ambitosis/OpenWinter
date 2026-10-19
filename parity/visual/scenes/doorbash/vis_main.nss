#include "visual_inc"
// A transition door and an ordinary door killed by damage (docs/INTER_AREA_MOVEMENT.md, "Killed doors"): how the
// broken doors look, the transition door's linked door, and the ordinary door going 2 s later; then the player is
// told to use the broken transition door.
void State(string label)
{
    vector p = GetPosition(GetFirstPC());
    object b = GetObjectByTag("bash");
    WriteTimestampedLogEntry("VISUAL|note|door|" + label + "|bash=" + IntToString(GetIsObjectValid(b)) + "," + IntToString(GetIsOpen(b))
        + "," + IntToString(GetCurrentHitPoints(b)) + "|dest=" + IntToString(GetIsOpen(GetObjectByTag("dest")))
        + "|plain=" + IntToString(GetIsObjectValid(GetObjectByTag("plain")))
        + "|pc=" + FloatToString(p.x, 0, 2) + "," + FloatToString(p.y, 0, 2) + "|action=" + IntToString(GetCurrentAction(GetFirstPC())));
}
void Kill(string t)
{
    object d = GetObjectByTag(t);
    SetPlotFlag(d, FALSE);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1000, DAMAGE_TYPE_MAGICAL), d);
}
void Use() { object pc = GetFirstPC(); AssignCommand(pc, ActionInteractObject(GetObjectByTag("bash"))); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN); SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetLocked(GetObjectByTag("bash"), FALSE); SetLocked(GetObjectByTag("dest"), FALSE);
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(4.0, State("closed")); DelayCommand(4.0, VShot("a_closed"));
    DelayCommand(5.0, Kill("bash")); DelayCommand(5.0, Kill("plain"));
    DelayCommand(5.3, VShot("b_dying"));
    DelayCommand(6.5, State("dead")); DelayCommand(6.5, VShot("c_dead"));
    DelayCommand(9.0, State("later")); DelayCommand(9.0, VShot("d_later"));
    DelayCommand(11.0, Use());
    DelayCommand(16.0, State("used")); DelayCommand(16.0, VShot("e_used"));
    DelayCommand(18.0, VDone());
}
