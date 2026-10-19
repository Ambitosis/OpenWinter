#include "visual_inc"

void Report(object pc, string phase)
{
    object item = GetLocalObject(pc, "item");
    vector v = GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|" + phase
        + "|valid=" + IntToString(GetIsObjectValid(item))
        + "|owned=" + IntToString(GetItemPossessor(item) == pc)
        + "|right=" + IntToString(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, pc) == item)
        + "|left=" + IntToString(GetItemInSlot(INVENTORY_SLOT_LEFTHAND, pc) == item)
        + "|action=" + IntToString(GetCurrentAction(pc))
        + "|x=" + FloatToString(v.x) + "|y=" + FloatToString(v.y));
}

void Prepare(object pc, string phase)
{
    AssignCommand(pc, ClearAllActions(TRUE));
    DestroyObject(GetLocalObject(pc, "item"));
    DestroyObject(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, pc));
    DestroyObject(GetItemInSlot(INVENTORY_SLOT_LEFTHAND, pc));
    VMovePC(pc, 20.0, 15.0, 0.0, 90.0);
    object item = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001",
        Location(GetArea(pc), Vector(23.0, 15.0, 0.0), 0.0));
    SetName(item, "Ground " + phase);
    SetTag(item, "RG_ITEM");
    SetLocalObject(pc, "item", item);
    Report(pc, phase + "_before");
}

void Root(float t, string name, int key)
{
    DelayCommand(t, WriteTimestampedLogEntry("VISUAL|hover|0.754,0.66"));
    DelayCommand(t + .3, WriteTimestampedLogEntry("VISUAL|mouse|0.754,0.66|down|right"));
    DelayCommand(t + 1.0, VShot(name));
    DelayCommand(t + 1.2, WriteTimestampedLogEntry("VISUAL|native-shot|" + name));
    DelayCommand(t + 2.0, WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"));
    DelayCommand(t + 4.5, WriteTimestampedLogEntry("VISUAL|mouse|0.754,0.66|up|right"));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    WriteTimestampedLogEntry("VISUAL|note|profile|race=" + IntToString(GetRacialType(pc))
        + "|wizard=" + IntToString(GetLevelByClass(CLASS_TYPE_WIZARD, pc)));
    DelayCommand(1.0, Prepare(pc, "right"));
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    Root(4.0, "a_right_root", 102);
    DelayCommand(6.1, Report(pc, "right_0.1"));
    DelayCommand(6.7, Report(pc, "right_0.7"));
    DelayCommand(7.5, Report(pc, "right_1.5"));
    DelayCommand(9.0, Report(pc, "right_3"));
    DelayCommand(10.0, Prepare(pc, "left"));
    DelayCommand(11.0, VCamera(pc, 90.0, 8.0, 60.0));
    Root(13.0, "b_left_root", 100);
    DelayCommand(15.1, Report(pc, "left_0.1"));
    DelayCommand(15.7, Report(pc, "left_0.7"));
    DelayCommand(16.5, Report(pc, "left_1.5"));
    DelayCommand(18.0, Report(pc, "left_3"));
    DelayCommand(19.0, Prepare(pc, "pickup"));
    DelayCommand(20.0, VCamera(pc, 90.0, 8.0, 60.0));
    Root(22.0, "c_pickup_root", 98);
    DelayCommand(24.1, Report(pc, "pickup_0.1"));
    DelayCommand(24.7, Report(pc, "pickup_0.7"));
    DelayCommand(25.5, Report(pc, "pickup_1.5"));
    DelayCommand(27.0, Report(pc, "pickup_3"));
    DelayCommand(29.0, AssignCommand(pc, ActionEquipItem(GetItemPossessedBy(pc, "RG_ITEM"), INVENTORY_SLOT_RIGHTHAND)));
    DelayCommand(31.0, Report(pc, "script_equip_control"));
    DelayCommand(32.0, VDone());
}
