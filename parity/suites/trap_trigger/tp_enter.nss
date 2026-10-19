#include "ptrap_inc"
void main()
{
    PTrapEv("enter:" + GetTag(OBJECT_SELF) + ":" + PTrapTag(GetEnteringObject()) + ":trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)));
}
