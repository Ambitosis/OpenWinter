// Visual parity harness: scene scripts set up a view and log "VISUAL|shot|<name>" when both
// engines should take a screenshot (tools/visual/visual.py watches the logs). Hold the scene
// still for a couple of seconds after each shot: the original client's log is polled.

// The line also carries where the player (the camera's anchor) stands, its facing and name, so the
// harness can check that both engines framed the same view.
void VShot(string sName)
{
    object oPC = GetFirstPC();
    vector v = GetPosition(oPC);
    WriteTimestampedLogEntry("VISUAL|shot|" + sName + "|" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2)
        + "|" + FloatToString(GetFacing(oPC), 0, 1) + "|" + GetName(oPC));
}

// The last shot was taken: the harness closes both engines.
void VDone()
{
    WriteTimestampedLogEntry("VISUAL|done|1");
}

// The camera orbits the player: fDirection as SetCameraFacing (0 = looking east,
// counter-clockwise), fDistance in metres, fPitch from straight down (0) to level (90).
void VCamera(object oPC, float fDirection, float fDistance, float fPitch)
{
    AssignCommand(oPC, SetCameraFacing(fDirection, fDistance, fPitch, CAMERA_TRANSITION_TYPE_SNAP));
}

// Common setup: cutscene mode (no GUI, no player control), a top-down camera and wide limits.
void VBegin(object oPC)
{
    SetCutsceneMode(oPC, TRUE, FALSE);
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(oPC, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(oPC, TRUE);
}

// Hides the player (the camera still orbits it): for views of the scenery alone.
void VHidePC(object oPC)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(VFX_DUR_CUTSCENE_INVISIBILITY), oPC);
}

// Moves the camera's anchor (the player) to a point in the area.
void VMovePC(object oPC, float fX, float fY, float fZ, float fFacing)
{
    location l = Location(GetArea(oPC), Vector(fX, fY, fZ), fFacing);
    AssignCommand(oPC, ClearAllActions(TRUE));
    AssignCommand(oPC, JumpToLocation(l));
}

// A creature from a standard blueprint, at an offset from the player, made neutral and idle.
object VSpawn(object oPC, string sResRef, float fDx, float fDy, float fFacing)
{
    vector v = GetPosition(oPC);
    location l = Location(GetArea(oPC), Vector(v.x + fDx, v.y + fDy, v.z), fFacing);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, l);
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetPlotFlag(o, TRUE);
    return o;
}

// A visual effect at an offset from the player.
void VFxAt(object oPC, int nVfx, float fDx, float fDy)
{
    vector v = GetPosition(oPC);
    ApplyEffectAtLocation(DURATION_TYPE_INSTANT, EffectVisualEffect(nVfx), Location(GetArea(oPC), Vector(v.x + fDx, v.y + fDy, v.z), 0.0));
}

// The standard set of views of the player's surroundings, starting after fStart seconds:
// a 3/4 view from the south, a closer, flatter view from the south-west and a steep overview,
// then VDone unless bDone is FALSE. Returns the time after the last shot. (Schedule VDone here:
// the original drops DelayCommand(VStandardViews(oPC), VDone()), whose delay is a call.)
float VStandardViews(object oPC, float fStart = 3.0, string sPrefix = "", int bDone = TRUE)
{
    float t = fStart;
    DelayCommand(t, VCamera(oPC, 90.0, 12.0, 55.0));
    DelayCommand(t + 2.0, VShot(sPrefix + "a_south"));
    DelayCommand(t + 4.0, VCamera(oPC, 45.0, 7.0, 72.0));
    DelayCommand(t + 6.0, VShot(sPrefix + "b_low"));
    DelayCommand(t + 8.0, VCamera(oPC, 300.0, 22.0, 25.0));
    DelayCommand(t + 10.0, VShot(sPrefix + "c_over"));
    if (bDone) DelayCommand(t + 12.0, VDone());
    return t + 12.0;
}

// A placeable from a standard blueprint at an offset from the player.
object VPlaceable(object oPC, string sResRef, float fDx, float fDy, float fFacing = 0.0)
{
    vector v = GetPosition(oPC);
    return CreateObject(OBJECT_TYPE_PLACEABLE, sResRef, Location(GetArea(oPC), Vector(v.x + fDx, v.y + fDy, v.z), fFacing));
}
