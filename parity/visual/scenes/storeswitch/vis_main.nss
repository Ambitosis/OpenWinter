#include "visual_inc"
// Opening a different store while one is open: whose 2 m boundary applies, the first store's or the second's?
void Mark(string label, object a, object b, object pc)
{
    vector p = GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|" + label + "|closedA=" + IntToString(GetLocalInt(a, "closed"))
        + "|closedB=" + IntToString(GetLocalInt(b, "closed")) + "|x=" + FloatToString(p.x) + "|y=" + FloatToString(p.y));
}
void Walk(object pc, float x) { AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(x, 25.0, 0.0), 0.0))); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    object a = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", GetLocation(pc), FALSE, "store_a");
    object b = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", Location(GetArea(pc), Vector(30.0, 27.0, 0.0), 0.0), FALSE, "store_b");
    SetEventScript(a, 14001, "st_closed"); SetEventScript(b, 14001, "st_closed");
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(4.0, OpenStore(a, pc));
    DelayCommand(6.0, Walk(pc, 31.5));
    DelayCommand(10.0, Mark("a_walk_15", a, b, pc)); DelayCommand(10.1, VShot("a_walk_15"));
    DelayCommand(12.0, OpenStore(b, pc));
    DelayCommand(14.0, Mark("b_opened_b", a, b, pc)); DelayCommand(14.1, VShot("b_opened_b"));
    DelayCommand(16.0, Walk(pc, 32.3));   // 2.3 m from A's opening point, 0.8 m from B's
    DelayCommand(20.0, Mark("c_walk_23", a, b, pc)); DelayCommand(20.1, VShot("c_walk_23"));
    DelayCommand(22.0, Walk(pc, 33.2));   // 1.7 m from B's
    DelayCommand(26.0, Mark("d_walk_32", a, b, pc)); DelayCommand(26.1, VShot("d_walk_32"));
    DelayCommand(28.0, Walk(pc, 34.0));   // 2.5 m from B's
    DelayCommand(32.0, Mark("e_walk_40", a, b, pc)); DelayCommand(32.1, VShot("e_walk_40"));
    DelayCommand(36.0, VDone());
}
