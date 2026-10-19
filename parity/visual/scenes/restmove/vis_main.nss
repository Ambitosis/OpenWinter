// Resting, then a move order interrupts it: the player should get up and walk, not slide in a frozen pose.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    DelayCommand(2.0, VCamera(oPC, 90.0, 6.0, 65.0));
    DelayCommand(3.0, AssignCommand(oPC, ActionRest()));
    DelayCommand(5.0, VShot("a_resting"));
    location l = Location(GetArea(oPC), GetPosition(oPC) + Vector(0.0, 6.0, 0.0), 90.0);
    DelayCommand(6.0, AssignCommand(oPC, ClearAllActions()));
    DelayCommand(6.0, AssignCommand(oPC, ActionMoveToLocation(l)));
    DelayCommand(6.8, VShot("b_moving_1"));
    DelayCommand(7.6, VShot("c_moving_2"));
    DelayCommand(11.0, VShot("d_arrived"));
    DelayCommand(13.0, VDone());
}
