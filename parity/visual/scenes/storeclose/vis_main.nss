#include "visual_inc"
void Mark(string label, object store, object pc)
{
    vector p = GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|" + label + "|closed=" + IntToString(GetLocalInt(store, "closed"))
        + "|x=" + FloatToString(p.x) + "|y=" + FloatToString(p.y));
}
void Walk(object pc, float x) { AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(x, 25.0, 0.0), 0.0))); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    object store = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", GetLocation(pc), FALSE, "fixture_store");
    SetEventScript(store, 14001, "st_closed");
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(4.0, OpenStore(store, pc));
    DelayCommand(6.0, AssignCommand(pc, SetFacing(180.0)));
    DelayCommand(8.0, Mark("turned", store, pc)); DelayCommand(8.1, VShot("a_turned"));
    DelayCommand(10.0, Walk(pc, 30.1));
    DelayCommand(13.0, Mark("walk_01", store, pc)); DelayCommand(13.1, VShot("b_walk_01"));
    DelayCommand(16.0, OpenStore(store, pc));
    DelayCommand(18.0, Walk(pc, 31.0));
    DelayCommand(21.0, Mark("walk_1", store, pc)); DelayCommand(21.1, VShot("c_walk_1"));
    DelayCommand(24.0, OpenStore(store, pc));
    DelayCommand(26.0, Walk(pc, 34.0));
    DelayCommand(30.0, Mark("walk_4", store, pc)); DelayCommand(30.1, VShot("d_walk_4"));
    DelayCommand(33.0, AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(48.0, 25.0, 0.0), 0.0))));
    DelayCommand(35.0, OpenStore(store, pc));
    DelayCommand(38.0, Mark("remote_open", store, pc)); DelayCommand(38.1, VShot("e_remote_open"));
    DelayCommand(40.0, Walk(pc, 48.2));
    DelayCommand(43.0, Mark("remote_walk", store, pc)); DelayCommand(43.1, VShot("f_remote_walk"));
    DelayCommand(46.0, Walk(pc, 50.5));
    DelayCommand(50.0, Mark("remote_boundary", store, pc)); DelayCommand(50.1, VShot("g_remote_boundary"));
    DelayCommand(56.0, VDone());
}
