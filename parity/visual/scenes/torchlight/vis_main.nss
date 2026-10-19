// Torch light calibration: a dark castle floor (no tile lights, no ambient) lit only by the torch an NPC holds,
// seen from above and from the side; a second NPC without a torch beside it casts a shadow (how high the light sits).
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    object o = VSpawn(oPC, "nw_bandit001", -5.0, 0.0, 270.0);
    object oTorch = CreateItemOnObject("nw_it_torch001", o);
    AssignCommand(o, ActionEquipItem(oTorch, INVENTORY_SLOT_LEFTHAND));
    VSpawn(oPC, "nw_bandit001", -3.0, 1.5, 270.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 18.0, 1.0));
    DelayCommand(5.0, VShot("top"));
    DelayCommand(7.0, VCamera(oPC, 90.0, 12.0, 60.0));
    DelayCommand(9.0, VShot("side"));
    DelayCommand(11.0, VCamera(oPC, 0.0, 9.0, 70.0));
    DelayCommand(13.0, VShot("east"));
    DelayCommand(15.0, VDone());
}
