// Standard creatures and part-based NPCs (heads, armour, tints) in a row behind the player, in daylight.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    // Back row: monsters. Front row: part-based humanoids facing the camera (south).
    VSpawn(oPC, "nw_goblina",  -6.0, 6.0, 270.0);
    VSpawn(oPC, "nw_orca",     -3.0, 6.0, 270.0);
    VSpawn(oPC, "nw_skeleton",  0.0, 6.0, 270.0);
    VSpawn(oPC, "nw_ogre01",    3.5, 6.5, 270.0);
    VSpawn(oPC, "nw_bearbrwn",  7.0, 6.0, 270.0);
    VSpawn(oPC, "nw_bandit001", -4.5, 2.5, 270.0);
    VSpawn(oPC, "nw_guard",     -1.5, 2.5, 270.0);
    VSpawn(oPC, "nw_elfmage001", 1.5, 2.5, 270.0);
    VSpawn(oPC, "nw_dwarfmerc001", 4.5, 2.5, 270.0);
    // The camera looks north, at the rows, from the south (the player stands in front, facing it).
    DelayCommand(3.0, VCamera(oPC, 90.0, 13.0, 68.0));
    DelayCommand(6.0, VShot("a_lineup"));
    DelayCommand(8.0, VCamera(oPC, 90.0, 3.0, 82.0));
    DelayCommand(10.0, VShot("b_pc_close"));
    DelayCommand(12.0, VCamera(oPC, 90.0, 7.0, 75.0));
    DelayCommand(14.0, VShot("c_npcs"));
    DelayCommand(16.0, VDone());
}
