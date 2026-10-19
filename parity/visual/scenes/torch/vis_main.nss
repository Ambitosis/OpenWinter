// Torches held in the off hand: two NPCs with a torch (one also with a weapon) beside one without, and a
// torch-bearer walking back and forth, on a dark castle floor. Compare the arm pose, the flame and the
// light each torch casts on the floor.
#include "visual_inc"

object Torch(object o)
{
    object oTorch = CreateItemOnObject("nw_it_torch001", o);
    AssignCommand(o, ActionEquipItem(oTorch, INVENTORY_SLOT_LEFTHAND));
    return oTorch;
}

void Patrol(object o, location lA, location lB)
{
    AssignCommand(o, ActionMoveToLocation(lA));
    AssignCommand(o, ActionMoveToLocation(lB));
    AssignCommand(o, ActionDoCommand(Patrol(o, lA, lB)));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    vector v = GetPosition(oPC);
    object oArea = GetArea(oPC);
    Torch(VSpawn(oPC, "nw_bandit001", -1.5, 2.5, 270.0));
    Torch(VSpawn(oPC, "nw_elfmage001", 1.5, 2.5, 270.0));
    VSpawn(oPC, "nw_dwarfmerc001", 4.5, 2.5, 270.0);
    object oWalker = VSpawn(oPC, "nw_elfmage001", -5.0, 8.0, 0.0);
    Torch(oWalker);
    DelayCommand(1.0, Patrol(oWalker, Location(oArea, Vector(v.x + 5.0, v.y + 8.0, v.z), 0.0), Location(oArea, Vector(v.x - 5.0, v.y + 8.0, v.z), 180.0)));
    // The camera looks north at the NPCs from the south.
    DelayCommand(4.0, VCamera(oPC, 90.0, 7.0, 75.0));
    DelayCommand(6.0, VShot("a_front"));
    DelayCommand(8.0, VCamera(oPC, 90.0, 4.0, 82.0));
    DelayCommand(10.0, VShot("b_close"));
    DelayCommand(12.0, VCamera(oPC, 0.0, 6.0, 78.0));
    DelayCommand(14.0, VShot("c_side"));
    DelayCommand(16.0, VCamera(oPC, 90.0, 16.0, 35.0));
    DelayCommand(18.0, VShot("d_top"));
    DelayCommand(20.0, VCamera(oPC, 90.0, 10.0, 70.0));
    DelayCommand(22.0, VShot("e_walk"));
    DelayCommand(23.0, VShot("f_walk"));
    DelayCommand(25.0, VDone());
}
