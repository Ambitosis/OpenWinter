// Near the bedroom's outside edge: the border cap crosses the player and must be keyholed.
// Compare defaults with --keyhole min-z-offset=6 (uncut, without moving the camera).
#include "visual_inc"

void View(object pc, float t, float dir, float dist, float pitch, string name)
{
    DelayCommand(t, VCamera(pc, dir, dist, pitch));
    DelayCommand(t + 2.0, VShot(name));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    View(pc, 3.0, 330.0, 9.0, 60.0, "a_edge_oblique");
    View(pc, 7.0, 330.0, 9.0, 70.0, "b_edge_low");
    View(pc, 11.0, 0.0, 9.0, 60.0, "c_edge_front");
    View(pc, 15.0, 35.0, 9.0, 60.0, "d_edge_reverse");
    DelayCommand(19.0, VMovePC(pc, 6.8, 4.0, 0.0, 90.0));
    View(pc, 20.0, 330.0, 9.0, 60.0, "e_room_oblique");
    DelayCommand(24.0, VDone());
}
