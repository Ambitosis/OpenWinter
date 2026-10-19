#include "visual_inc"

void Item(object pc, string resref, string name, float x)
{
    object item = CreateObject(OBJECT_TYPE_ITEM, resref,
        Location(GetArea(pc), Vector(x, 15.0, 0.0), 0.0));
    SetName(item, name);
    vector p = GetPosition(item);
    WriteTimestampedLogEntry("VISUAL|note|item|" + name
        + "|valid=" + IntToString(GetIsObjectValid(item))
        + "|resref=" + GetResRef(item)
        + "|base=" + IntToString(GetBaseItemType(item))
        + "|position=" + FloatToString(p.x) + "," + FloatToString(p.y) + "," + FloatToString(p.z));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    WriteTimestampedLogEntry("VISUAL|note|profile|pc=" + IntToString(GetIsPC(pc)));
    DelayCommand(.5, Item(pc, "nw_it_mpotion001", "Potion Target", 23.0));
    DelayCommand(.5, Item(pc, "nw_wamar001", "Arrow Target", 17.0));
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(4.0, VShot("positions"));
    DelayCommand(8.0, VDone());
}
