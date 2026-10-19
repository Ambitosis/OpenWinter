// The original's item windows for the classic inventory (Classic UI mode): a store opened by script with the
// inventory beside it, then a chest opened by the player (its contents take the inventory's top half), and the
// inventory alone on a later page. OpenWinter's side: --ui classic with --capture-classic-inventory.
#include "visual_inc"

void Tap(int nVk)
{
    WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap");
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetPlotFlag(oPC, TRUE);
    DelayCommand(2.0, VCamera(oPC, 90.0, 8.0, 60.0));
    object oStore = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", GetLocation(oPC));
    DelayCommand(4.0, OpenStore(oStore, oPC));
    DelayCommand(7.0, VShot("a_store"));
    DelayCommand(8.0, Tap(27));
    // A chest beside the player with a few things in it.
    object oChest = VPlaceable(oPC, "plc_chest1", 1.2, 0.0, 180.0);
    CreateItemOnObject("nw_wswdg001", oChest);
    CreateItemOnObject("nw_it_mpotion001", oChest, 3);
    CreateItemOnObject("nw_aarcl001", oChest);
    DelayCommand(11.0, AssignCommand(oPC, ActionInteractObject(oChest)));
    DelayCommand(16.0, VShot("b_container"));
    DelayCommand(17.0, Tap(27));
    DelayCommand(19.0, Tap(73));
    DelayCommand(21.0, Tap(221));
    DelayCommand(23.0, VShot("c_page2"));
    DelayCommand(24.0, Tap(73));
    DelayCommand(26.0, VDone());
}
