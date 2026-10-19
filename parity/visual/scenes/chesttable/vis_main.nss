#include "visual_inc"

void Observe(object pc, object chest, int n = 0)
{
    vector v = GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|table|" + IntToString(n) + "|"
        + FloatToString(v.x, 0, 3) + "," + FloatToString(v.y, 0, 3) + "," + FloatToString(v.z, 0, 3)
        + "|distance=" + FloatToString(GetDistanceBetween(pc, chest), 0, 3)
        + "|action=" + IntToString(GetCurrentAction(pc)) + "|open=" + IntToString(GetIsOpen(chest)));
    if (n < 80) DelayCommand(0.1, Observe(pc, chest, n + 1));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    // Put the chest at table height to exercise an approach obstructed by furniture.
    VPlaceable(pc, "plc_table", 0.0, 2.0);
    vector v = GetPosition(pc);
    object chest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1",
        Location(GetArea(pc), Vector(v.x, v.y + 2.0, v.z + 1.0), 0.0));
    SetTag(chest, "table_chest");
    SetLocked(chest, FALSE);
    CreateItemOnObject("nw_it_mpotion001", chest);
    DelayCommand(3.0, VCamera(pc, 90.0, 9.0, 55.0));
    DelayCommand(5.0, VShot("a_before"));
    DelayCommand(6.0, WriteTimestampedLogEntry("VISUAL|click|0.50,0.28|table_chest"));
    DelayCommand(6.0, Observe(pc, chest));
    DelayCommand(7.0, VShot("b_after_one_second"));
    DelayCommand(9.0, VShot("c_after_three_seconds"));
    DelayCommand(15.0, VDone());
}
