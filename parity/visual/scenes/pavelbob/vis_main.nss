#include "visual_inc"

// Does Pavel's head move more while he is in a conversation (reported: it bobs throughout, silent
// lines too)? Close side-on view, shot series: idle, silent line, voiced line, after the end. Measured:
// head vs chest moves at most 2-3 px (head ~60 px) and repeats every 2.0 s (pause1) on both lines: no talk bob.
void Tap(int vk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(vk) + "|tap"); }
// Shots 0.4 s apart; faster back-to-back window grabs froze the original client (tried 0.1 and 0.25 s).
void Burst(string name, int count, float at)
{
    int i;
    for (i = 0; i < count; i++) DelayCommand(at + 0.4 * IntToFloat(i), VShot(name + "_0" + IntToString(i)));
}
void Tick(int n) { WriteTimestampedLogEntry("VISUAL|note|tick " + IntToString(n)); }
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    object pavel = GetObjectByTag("M1Q0APavel");
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    int i;
    for (i = 1; i <= 32; i++) DelayCommand(IntToFloat(i), Tick(i));
    // West of Pavel, facing him; the camera looks north from the south side, so both stand in profile.
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), GetPosition(pavel) + Vector(-1.6, 0.0, 0.0), 0.0)));
    DelayCommand(3.0, VCamera(pc, 90.0, 3.5, 82.0));
    DelayCommand(5.0, VShot("a_frame"));
    Burst("a_idle", 8, 7.0);
    DelayCommand(11.0, AssignCommand(pc, ActionStartConversation(pavel, "vis_pbob")));
    DelayCommand(11.5, VCamera(pc, 90.0, 3.5, 82.0));
    Burst("b_silent", 9, 14.0);
    DelayCommand(18.0, Tap(49));
    Burst("c_voice", 9, 19.0);
    DelayCommand(23.0, Tap(49));
    Burst("d_after", 8, 25.0);
    DelayCommand(29.0, VShot("e_end"));
    DelayCommand(31.0, VDone());
}
