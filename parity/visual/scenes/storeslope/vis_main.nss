#include "visual_inc"
// The store's 2 m walk-away on a 32 degree ramp (m0q1e, rising towards +y from y=31): 3-D or ground distance?
void Mark(string label, object store, object pc)
{
    vector p = GetPosition(pc); vector s = GetPosition(store);
    WriteTimestampedLogEntry("VISUAL|note|" + label + "|closed=" + IntToString(GetLocalInt(store, "closed"))
        + "|x=" + FloatToString(p.x) + "|y=" + FloatToString(p.y) + "|z=" + FloatToString(p.z)
        + "|sz=" + FloatToString(s.z) + "|dist=" + FloatToString(GetDistanceBetween(pc, store)));
}
void Walk(object pc, float y) { AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(33.8, y, 0.0), 90.0))); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    object store = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", GetLocation(pc), FALSE, "fixture_store");
    SetEventScript(store, 14001, "st_closed");
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(2.0, VCamera(pc, 0.0, 12.0, 60.0));
    DelayCommand(4.0, OpenStore(store, pc));
    DelayCommand(6.0, Mark("a_open", store, pc));
    DelayCommand(7.0, Walk(pc, 32.0));
    DelayCommand(11.0, Mark("b_up_10", store, pc)); DelayCommand(11.1, VShot("b_up_10"));
    DelayCommand(13.0, Walk(pc, 32.75));  // ground 1.75 m, 3-D about 2.07 m
    DelayCommand(17.0, Mark("c_up_175", store, pc)); DelayCommand(17.1, VShot("c_up_175"));
    DelayCommand(19.0, Walk(pc, 33.3));   // ground 2.3 m
    DelayCommand(23.0, Mark("d_up_230", store, pc)); DelayCommand(23.1, VShot("d_up_230"));
    DelayCommand(27.0, VDone());
}
