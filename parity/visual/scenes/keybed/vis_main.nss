// Prelude bedroom: black border caps, a hole without a central obstruction, and bedposts versus mattress.
// Repeat with --keyhole min-z-offset=0 and min-z-offset=6 to isolate the height rule and uncut geometry.
#include "visual_inc"

void View(object oPC, float t, float dir, float dist, float pitch, string name)
{
    DelayCommand(t, VCamera(oPC, dir, dist, pitch));
    DelayCommand(t + 2.0, VShot(name));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    View(oPC, 3.0, 0.0, 7.0, 80.0, "a_bed_low");
    View(oPC, 7.0, 25.0, 9.0, 70.0, "b_bed_oblique");
    View(oPC, 11.0, 0.0, 14.0, 50.0, "c_wall_back");
    DelayCommand(15.0, VMovePC(oPC, 6.8, 4.0, 0.0, 0.0));
    View(oPC, 16.0, 35.0, 14.0, 55.0, "d_room_wide");
    View(oPC, 20.0, 200.0, 8.0, 70.0, "e_open_sightline");
    View(oPC, 24.0, 205.0, 8.0, 70.0, "f_open_sightline_turn");
    DelayCommand(28.0, VDone());
}
