#include "mx_plan"
// Logs and files every spawned creature under the running test (module string "cur").
void main()
{
    object m = GetModule();
    string sLabel = GetLocalString(m, "cur");
    int n = GetLocalInt(m, "n_" + sLabel) + 1;
    SetLocalInt(m, "n_" + sLabel, n);
    SetLocalObject(m, "o_" + sLabel + "_" + IntToString(n), OBJECT_SELF);
    vector v = GetPosition(OBJECT_SELF), c = MxSpot(sLabel), p = GetPosition(GetFirstPC());
    WriteTimestampedLogEntry("VISUAL|note|mx|spawn|" + sLabel + "|" + IntToString(n) + "|" + GetTag(OBJECT_SELF)
        + "|spot=" + FloatToString(v.x - c.x, 0, 1) + "," + FloatToString(v.y - c.y, 0, 1)
        + "|pc=" + FloatToString(v.x - p.x, 0, 1) + "," + FloatToString(v.y - p.y, 0, 1)
        + "|area=" + GetTag(GetArea(OBJECT_SELF)) + "|pcarea=" + GetTag(GetArea(GetFirstPC())));
}
