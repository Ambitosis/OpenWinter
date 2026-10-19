#include "ew_inc"
// Who enters an encounter's square, and where.
void main()
{
    object o = GetEnteringObject();
    vector v = GetPosition(o);
    Log("enter|" + GetTag(OBJECT_SELF) + "|" + GetName(o) + "|at=" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2)
        + "|cur=" + IntToString(GetEncounterSpawnsCurrent()) + "|active=" + IntToString(GetEncounterActive()));
}
