#include "visual_inc"
void Click(float x, float y) {
    string xy = FloatToString(x / 1280.0,0,6) + "," + FloatToString(y / 720.0,0,6);
    WriteTimestampedLogEntry("VISUAL|mouse|" + xy + "|up");
    DelayCommand(0.25, WriteTimestampedLogEntry("VISUAL|mouse|" + xy + "|down"));
    DelayCommand(0.4, WriteTimestampedLogEntry("VISUAL|mouse|" + xy + "|up"));
}
void Stock(object store, object pc)
{
    CreateItemOnObject("nw_aarcl001", store);
    CreateItemOnObject("nw_wswdg001", store);
    CreateItemOnObject("nw_it_mpotion001", store);
    CreateItemOnObject("nw_it_mring020", store);
    CreateItemOnObject("nw_it_medkit001", store);
    CreateItemOnObject("nw_wswdg001", pc);
    CreateItemOnObject("nw_it_mpotion001", pc, 3);
    WriteTimestampedLogEntry("VISUAL|note|stock|valid=" + IntToString(GetIsObjectValid(store)) + "|tag=" + GetTag(store)
        + "|by_tag=" + IntToString(GetObjectByTag("fixture_store") == store));
    object item = GetFirstItemInInventory(store);
    while (GetIsObjectValid(item)) {
        WriteTimestampedLogEntry("VISUAL|note|stock_item|" + GetResRef(item) + "|owner=" + IntToString(GetItemPossessor(item) == store));
        item = GetNextItemInInventory(store);
    }
}
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE); GiveGoldToCreature(pc, 10000);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    object store = CreateObject(OBJECT_TYPE_STORE, "st_empty", GetLocation(pc), FALSE, "fixture_store");
    object item = GetFirstItemInInventory(store);
    while (GetIsObjectValid(item)) { DestroyObject(item); item = GetNextItemInInventory(store); }
    SetEventScript(store, 14001, "st_closed");
    DelayCommand(1.0, Stock(store, pc));
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(4.0, OpenStore(store, pc));
    DelayCommand(6.0, Click(32.0, 444.0)); DelayCommand(8.0, VShot("a_eye"));
    DelayCommand(10.0, Click(48.0, 369.0)); DelayCommand(12.0, VShot("b_examine"));
    DelayCommand(15.0, VDone());
}
