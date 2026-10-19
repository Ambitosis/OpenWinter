// OnBlocked: who blocked whom, and when.
#include "parity_inc"
#include "parity_time"
void main()
{
    string k = "blocked." + GetTag(OBJECT_SELF);
    PLog(k, GetTag(GetBlockingDoor()));
    PLogF(k + ".t", PSince(), 0.25);
    PLogI(k + ".action", GetCurrentAction(OBJECT_SELF));
}
