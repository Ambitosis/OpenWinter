#include "parity_inc"

void main()
{
    object o = GetEnteringObject();
    if (GetStringLeft(GetTag(o), 2) == "HB") return;
    PLog("order", "enter:" + GetTag(o) + " spawned=" + IntToString(GetLocalInt(o, "SPAWNED")));
}
