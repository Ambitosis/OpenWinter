// Items the player drops (ActionPutDownItem): where they land and which way they face, against the player's
// facing. No cutscene mode (leaving it clears the player's queued actions). The player drops a longsword facing
// north (90), walks off, turns east (0) and drops another. Beside them, made by script: armour, cloak, helmet
// and a bag (amulet) at facing 90, and armour at facing 0 (docs/GROUND_ITEMS.md).
#include "visual_inc"

void Note(object o, string what)
{
    vector v = GetPosition(o);
    WriteTimestampedLogEntry("VISUAL|note|item|" + what + "|valid=" + IntToString(GetIsObjectValid(o)) + "|onGround=" + IntToString(!GetIsObjectValid(GetItemPossessor(o)))
        + "|facing=" + FloatToString(GetFacing(o), 0, 1) + "|pos=" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2));
}

void Pc(string what)
{
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|pc|" + what + "|facing=" + FloatToString(GetFacing(pc), 0, 1) + "|pos=" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2));
}

void Drop(string key)
{
    object pc = GetFirstPC();
    object o = CreateItemOnObject("nw_wswls001", pc);
    SetLocalObject(pc, key, o);
    Pc(key + "_before");
    AssignCommand(pc, ActionPutDownItem(o));
}

void Put(string resref, float x, float y, float facing, string what)
{
    object o = CreateObject(OBJECT_TYPE_ITEM, resref, Location(GetArea(GetFirstPC()), Vector(x, y, 0.0), facing));
    Note(o, what);
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    Put("nw_aarcl001", 18.0, 22.0, 90.0, "armour90");
    Put("x2_it_mcloak001", 19.5, 22.0, 90.0, "cloak90");
    Put("nw_arhe001", 21.0, 22.0, 90.0, "helmet90");
    Put("nw_it_mneck001", 22.5, 22.0, 90.0, "bag90");
    Put("nw_aarcl001", 24.0, 22.0, 0.0, "armour0");
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, SetFacing(90.0));
    DelayCommand(1.0, VCamera(pc, 90.0, 6.0, 1.0));
    DelayCommand(2.0, Drop("drop90"));
    DelayCommand(5.0, Note(GetLocalObject(pc, "drop90"), "drop90"));
    DelayCommand(5.0, Pc("drop90_after"));
    DelayCommand(5.5, AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(23.0, 20.0, 0.0), 0.0))));
    DelayCommand(9.0, AssignCommand(pc, SetFacing(0.0)));
    DelayCommand(10.0, Drop("drop0"));
    DelayCommand(13.0, Note(GetLocalObject(pc, "drop0"), "drop0"));
    DelayCommand(13.0, Pc("drop0_after"));
    DelayCommand(13.5, AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(21.0, 19.0, 0.0), 0.0))));
    DelayCommand(17.0, VCamera(pc, 90.0, 6.0, 1.0));
    DelayCommand(19.0, VShot("a_top"));
    DelayCommand(20.0, VCamera(pc, 90.0, 5.0, 65.0));
    DelayCommand(22.0, VShot("b_low"));
    DelayCommand(24.0, VDone());
}
