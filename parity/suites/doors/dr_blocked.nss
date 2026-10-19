#include "parity_inc"

void main()
{
    PLog(GetTag(OBJECT_SELF) + ".blocked", GetTag(GetBlockingDoor()));
}
