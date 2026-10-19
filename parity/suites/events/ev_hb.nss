#include "parity_inc"
#include "parity_time"

// Creature beats only feed statistics: their timing is jittered in the original.
void Stat(object o, string sGroup)
{
    object m = GetModule();
    float t = PSince();
    float last = GetLocalFloat(o, "HB_LAST");
    SetLocalFloat(o, "HB_LAST", t);
    string k = "HB_" + sGroup;
    if (last == 0.0)
    {
        float first = t - GetLocalFloat(m, "HB_CREATED");
        SetLocalFloat(m, k + "_FSUM", GetLocalFloat(m, k + "_FSUM") + first);
        SetLocalInt(m, k + "_FN", GetLocalInt(m, k + "_FN") + 1);
        if (GetLocalInt(m, k + "_FN") == 1 || first < GetLocalFloat(m, k + "_FMIN")) SetLocalFloat(m, k + "_FMIN", first);
        if (first > GetLocalFloat(m, k + "_FMAX")) SetLocalFloat(m, k + "_FMAX", first);
        return;
    }
    float d = t - last;
    SetLocalFloat(m, k + "_SUM", GetLocalFloat(m, k + "_SUM") + d);
    SetLocalInt(m, k + "_N", GetLocalInt(m, k + "_N") + 1);
    if (GetLocalInt(m, k + "_N") == 1 || d < GetLocalFloat(m, k + "_MIN")) SetLocalFloat(m, k + "_MIN", d);
    if (d > GetLocalFloat(m, k + "_MAX")) SetLocalFloat(m, k + "_MAX", d);
}

void main()
{
    object o = OBJECT_SELF;
    if (GetLocalInt(GetModule(), "PARITY_STOP")) return;
    string tag = GetTag(o);
    if (GetStringLeft(tag, 4) == "HBN_") { Stat(o, "NORMAL"); return; }
    if (GetStringLeft(tag, 4) == "HBV_") { Stat(o, "VERYLOW"); return; }
    if (GetObjectType(o) == OBJECT_TYPE_CREATURE) return;
    string k = o == GetModule() ? "module" : o == GetArea(GetFirstObjectInArea(o)) ? "area" : tag;
    PLogF("hb." + k, PSince(), 0.15);
}
