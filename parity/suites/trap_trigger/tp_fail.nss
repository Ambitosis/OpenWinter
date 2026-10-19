#include "ptrap_inc"
void main()
{
    PTrapEv("fail:" + GetTag(OBJECT_SELF) + ":" + PTrapTag(GetClickingObject()) + ":trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)));
}
