#include "ptrap_inc"
void main()
{
    PTrapEv("disarm:" + GetTag(OBJECT_SELF) + ":by=" + PTrapTag(GetLastDisarmed()) + ":trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)));
}
