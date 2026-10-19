#include "ptrap_inc"
void main()
{
    PTrapEv("open:" + GetTag(OBJECT_SELF) + ":" + PTrapTag(GetLastOpenedBy()) + ":trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)));
}
