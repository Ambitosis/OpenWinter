// Reads the caller's killer.
#include "parity_inc"

void main()
{
    object k = GetLastKiller();
    PLog("killer." + GetTag(OBJECT_SELF), GetIsObjectValid(k) ? GetTag(k) : "none");
}
