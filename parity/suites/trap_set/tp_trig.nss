#include "ptrap_inc"
void main()
{
    PTrapEv("trig:" + GetTag(OBJECT_SELF) + ":enter=" + PTrapTag(GetEnteringObject()) + ":click=" + PTrapTag(GetClickingObject())
        + ":trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)) + ":active=" + IntToString(GetTrapActive(OBJECT_SELF)));
}
