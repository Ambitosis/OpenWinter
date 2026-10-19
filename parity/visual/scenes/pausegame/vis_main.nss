#include "visual_inc"
// The pause key (Space): what the chat window says and how, and that game time stops. The unpause tap comes from
// the harness's own clock (keyshot), since a paused game runs no DelayCommand.
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Tick(int i) { Log("note|pausegame|tick|" + IntToString(i) + "|pause=" + IntToString(GetGamePauseState())); DelayCommand(0.5, Tick(i + 1)); }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 55.0));
 DelayCommand(1.5, Tick(0));
 DelayCommand(2.0, AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(30.0, 30.0, 0.0), 0.0))));
 DelayCommand(3.0, VShot("a_walking"));
 // Space, a shot 3 s later on the harness clock, then Space again.
 DelayCommand(4.0, Log("keyshot|32|b_paused|3.0|32"));
 DelayCommand(6.0, VShot("c_after_unpause"));
 DelayCommand(9.0, VDone());
}
