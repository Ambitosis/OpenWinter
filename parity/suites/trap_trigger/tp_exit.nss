#include "ptrap_inc"
void main()
{
    PTrapEv("exit:" + GetTag(OBJECT_SELF) + ":" + PTrapTag(GetExitingObject()) + ":trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)));
}
