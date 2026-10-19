#include "parity_inc"

void main()
{
    object o = GetExitingObject();
    PLog("event", "exit:" + GetTag(o) + " valid=" + IntToString(GetIsObjectValid(o)) + " area=" + IntToString(GetIsObjectValid(GetArea(o))));
}
