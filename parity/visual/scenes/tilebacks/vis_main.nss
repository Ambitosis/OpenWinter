#include "visual_inc"
void View(object pc, float t, float angle, string name)
{
    DelayCommand(t, VCamera(pc, angle, 22.0, 55.0));
    DelayCommand(t+2.0, VShot(name));
}
void main()
{
    object pc=GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    VHidePC(pc);
    View(pc, 3.0, 0.0, "hidden_000");
    View(pc, 7.0, 45.0, "hidden_045");
    View(pc, 11.0, 90.0, "hidden_090");
    View(pc, 15.0, 135.0, "hidden_135");
    View(pc, 19.0, 180.0, "hidden_180");
    View(pc, 23.0, 225.0, "hidden_225");
    View(pc, 27.0, 270.0, "hidden_270");
    View(pc, 31.0, 315.0, "hidden_315");
    DelayCommand(35.0, ExploreAreaForPlayer(GetArea(pc),pc,TRUE));
    View(pc, 36.0, 0.0, "known_000");
    View(pc, 40.0, 45.0, "known_045");
    View(pc, 44.0, 90.0, "known_090");
    View(pc, 48.0, 135.0, "known_135");
    View(pc, 52.0, 180.0, "known_180");
    View(pc, 56.0, 225.0, "known_225");
    View(pc, 60.0, 270.0, "known_270");
    View(pc, 64.0, 315.0, "known_315");
    DelayCommand(68.0, VDone());
}
