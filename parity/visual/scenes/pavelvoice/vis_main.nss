#include "visual_inc"

void Tap(int vk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(vk) + "|tap"); }
void Shots(string name, float t)
{
    DelayCommand(t, VShot(name + "_0"));
    DelayCommand(t + 0.4, VShot(name + "_1"));
    DelayCommand(t + 0.8, VShot(name + "_2"));
    DelayCommand(t + 1.2, VShot(name + "_3"));
}
void Move(object pc, object npc)
{
    AssignCommand(pc, ClearAllActions());
    AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), GetPosition(npc) + Vector(0.0, -3.0, 0.0), 0.0)));
}
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    object pavel = GetObjectByTag("M1Q0APavel");
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), GetPosition(pavel) + Vector(-2.0, 0.0, 0.0), 0.0)));
    DelayCommand(3.0, VCamera(pc, 20.0, 5.5, 70.0));
    DelayCommand(5.0, AssignCommand(pc, ActionStartConversation(pavel, "vis_pvoice")));
    Shots("a_silent", 7.0);
    DelayCommand(10.0, Tap(49));
    Shots("b_voice", 11.0);
    DelayCommand(13.0, VCamera(pc, 200.0, 5.5, 70.0));
    Shots("c_voice", 14.0);
    DelayCommand(17.0, Move(pc, pavel));
    Shots("d_moving", 17.2);
    DelayCommand(21.0, VShot("e_after_move"));
    DelayCommand(23.0, Tap(49));
    DelayCommand(25.0, VShot("f_ended"));
    DelayCommand(27.0, VDone());
}
