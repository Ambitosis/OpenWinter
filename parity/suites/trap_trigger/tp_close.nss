#include "ptrap_inc"
void main()
{
    PTrapEv("close:" + GetTag(OBJECT_SELF) + ":" + PTrapTag(GetLastClosedBy()) + ":trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)));
}
