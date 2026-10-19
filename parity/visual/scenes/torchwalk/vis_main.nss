// Torch-bearers on the move, close up: one walking and one running back and forth across the view, a third
// walking towards the camera. Compare the arm that holds the torch while moving.
#include "visual_inc"

object Torch(object o)
{
    object oTorch = CreateItemOnObject("nw_it_torch001", o);
    AssignCommand(o, ActionEquipItem(oTorch, INVENTORY_SLOT_LEFTHAND));
    return oTorch;
}

void Patrol(object o, location lA, location lB, int bRun)
{
    AssignCommand(o, ActionMoveToLocation(lA, bRun));
    AssignCommand(o, ActionMoveToLocation(lB, bRun));
    AssignCommand(o, ActionDoCommand(Patrol(o, lA, lB, bRun)));
}

location At(object oPC, float fDx, float fDy)
{
    vector v = GetPosition(oPC);
    return Location(GetArea(oPC), Vector(v.x + fDx, v.y + fDy, v.z), 0.0);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    object oWalker = VSpawn(oPC, "nw_bandit001", -4.0, 2.0, 0.0);
    Torch(oWalker);
    object oRunner = VSpawn(oPC, "nw_bandit001", 6.0, 5.0, 180.0);
    Torch(oRunner);
    object oTowards = VSpawn(oPC, "nw_bandit001", 1.5, 9.0, 270.0);
    Torch(oTowards);
    DelayCommand(1.0, Patrol(oWalker, At(oPC, 4.0, 2.0), At(oPC, -4.0, 2.0), FALSE));
    DelayCommand(1.0, Patrol(oRunner, At(oPC, -6.0, 5.0), At(oPC, 6.0, 5.0), TRUE));
    DelayCommand(1.0, Patrol(oTowards, At(oPC, 1.5, 0.5), At(oPC, 1.5, 9.0), FALSE));
    DelayCommand(3.0, VCamera(oPC, 90.0, 6.0, 80.0));
    DelayCommand(5.0, VShot("a_move"));
    DelayCommand(6.0, VShot("b_move"));
    DelayCommand(7.0, VShot("c_move"));
    DelayCommand(8.0, VShot("d_move"));
    DelayCommand(9.0, VShot("e_move"));
    DelayCommand(10.0, VShot("f_move"));
    DelayCommand(12.0, VDone());
}
