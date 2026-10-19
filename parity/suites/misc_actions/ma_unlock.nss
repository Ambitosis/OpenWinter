#include "parity_inc"
#include "parity_time"

void main()
{
    PLog("door.event", "unlock");
    PLogF("door.unlock.time", PSince() - GetLocalFloat(GetModule(), "T_DOOR"), 0.12);
}
