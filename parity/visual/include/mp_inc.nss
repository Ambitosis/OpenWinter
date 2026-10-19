// Multiplayer sync harness (tools/multiplayer/mp.py, docs/MULTIPLAYER_HARNESS.md): helpers for scenes two players
// join on one server, run by the server's own scripts so the same scene drives the original (nwserver + two nwmain)
// and OpenWinter (openwinter-server + two clients).
//   MPShot(name)  a checkpoint: OpenWinter's server and both clients record what they hold and show; the original's
//                 harness takes both clients' screenshots. Also logs every player's state as notes (both engines).
//   MPNote(text)  a reading kept in notes.txt.
//   MPDo(player, "verb|args")  client input for one player (OpenWinter only; the original's harness ignores it).
//   MPDone()      the end: every process closes.
// Players are told apart by their player name (the login name: MPA, MPB, ...).

void MPNote(string sText) { WriteTimestampedLogEntry("VISUAL|note|" + sText); }

void MPDo(string sPlayer, string sInput) { WriteTimestampedLogEntry("VISUAL|do|" + sPlayer + "|" + sInput); }

void MPDone() { WriteTimestampedLogEntry("VISUAL|done|1"); }

// The PC logged in as sPlayer (OBJECT_INVALID when none).
object MPPlayer(string sPlayer)
{
    object o = GetFirstPC();
    while (GetIsObjectValid(o))
    {
        if (GetPCPlayerName(o) == sPlayer) return o;
        o = GetNextPC();
    }
    return OBJECT_INVALID;
}

int MPCountPCs()
{
    int n = 0;
    object o = GetFirstPC();
    while (GetIsObjectValid(o)) { n++; o = GetNextPC(); }
    return n;
}

string MPF(float f) { return FloatToString(f, 0, 2); }

string MPPos(object o)
{
    vector v = GetPosition(o);
    return MPF(v.x) + "," + MPF(v.y) + "," + MPF(v.z);
}

// One player's state as scripts see it: area, position, facing, hit points, experience, party leader, combat.
string MPState(object o)
{
    return GetPCPlayerName(o) + "=" + GetTag(GetArea(o)) + "@" + MPPos(o) + "/" + FloatToString(GetFacing(o), 0, 0)
        + "|hp=" + IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o))
        + "|xp=" + IntToString(GetXP(o)) + "|leader=" + GetPCPlayerName(GetFactionLeader(o))
        + "|action=" + IntToString(GetCurrentAction(o)) + "|combat=" + IntToString(GetIsInCombat(o));
}

// A checkpoint: every player's state as a note, then the shot marker.
void MPShot(string sName)
{
    object o = GetFirstPC();
    while (GetIsObjectValid(o))
    {
        MPNote(sName + "|" + MPState(o));
        o = GetNextPC();
    }
    WriteTimestampedLogEntry("VISUAL|shot|" + sName);
}

// Every PC's camera: SetCameraFacing (0 = looking east), distance, pitch from straight down (0) to level (90).
void MPCameras(float fDirection, float fDistance, float fPitch)
{
    object o = GetFirstPC();
    while (GetIsObjectValid(o))
    {
        AssignCommand(o, SetCameraFacing(fDirection, fDistance, fPitch, CAMERA_TRANSITION_TYPE_SNAP));
        o = GetNextPC();
    }
}

void MPJump(object o, float fX, float fY, float fFacing)
{
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, JumpToLocation(Location(GetArea(o), Vector(fX, fY, 0.0), fFacing)));
}
