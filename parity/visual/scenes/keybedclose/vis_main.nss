// Back out of the bedroom with a low camera. With --keyhole min-z-offset=6, the black border appears
// only once the camera rises above the authored cap, between the 10 m and 12 m views on the original.
#include "visual_inc"
void View(object pc, float t, float dist, string name)
{
    DelayCommand(t, VCamera(pc, 0.0, dist, 80.0));
    DelayCommand(t + 1.5, VShot(name));
}
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    View(pc, 3.0, 8.0, "d80");
    View(pc, 6.0, 9.0, "d90");
    View(pc, 9.0, 10.0, "d100");
    View(pc, 12.0, 12.0, "d120");
    View(pc, 15.0, 16.0, "d160");
    View(pc, 18.0, 20.0, "d200");
    DelayCommand(21.0, VDone());
}
