#include "ptrap_inc"
void main()
{
    PTrapEv("used:" + GetTag(OBJECT_SELF) + ":" + PTrapTag(GetLastUsedBy()) + ":trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)));
}
