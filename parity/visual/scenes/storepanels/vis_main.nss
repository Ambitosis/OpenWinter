#include "visual_inc"
void Tap(int vk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(vk) + "|tap"); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    object store = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", GetLocation(pc), FALSE, "fixture_store");
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(3.0, Tap(77)); DelayCommand(5.0, VShot("a_map"));
    DelayCommand(7.0, OpenStore(store, pc)); DelayCommand(9.0, VShot("b_store_after_map"));
    DelayCommand(11.0, Tap(67)); DelayCommand(13.0, VShot("c_character"));
    DelayCommand(15.0, Tap(74)); DelayCommand(17.0, VShot("d_journal"));
    DelayCommand(19.0, Tap(66)); DelayCommand(21.0, VShot("e_spellbook"));
    DelayCommand(23.0, Tap(73)); DelayCommand(25.0, VShot("f_inventory_toggle"));
    DelayCommand(27.0, OpenStore(store, pc)); DelayCommand(29.0, VShot("g_reopened"));
    DelayCommand(31.0, Tap(77)); DelayCommand(33.0, VShot("h_map_after_store"));
    DelayCommand(36.0, VDone());
}
