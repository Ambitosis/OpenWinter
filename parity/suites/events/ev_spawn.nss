#include "parity_inc"

void main()
{
    SetLocalInt(OBJECT_SELF, "SPAWNED", 1);
    if (GetStringLeft(GetTag(OBJECT_SELF), 2) == "HB") return;
    PLog("order", "spawn:" + GetTag(OBJECT_SELF) + " area=" + IntToString(GetIsObjectValid(GetArea(OBJECT_SELF)))
        + " local=" + IntToString(GetLocalInt(OBJECT_SELF, "SET_AFTER")));
}
