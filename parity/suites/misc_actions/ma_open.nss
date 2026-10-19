#include "parity_inc"
#include "parity_time"

void main()
{
    PLog("door.event", "open");
    PLogF("door.open.time", PSince() - GetLocalFloat(GetModule(), "T_DOOR"), 0.12);
}
