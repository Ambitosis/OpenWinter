#include "parity_inc"
#include "parity_time"

void main()
{
    PLog(GetTag(OBJECT_SELF) + ".event", "close by=" + GetTag(GetLastClosedBy()) + " open=" + IntToString(GetIsOpen(OBJECT_SELF))
        + " locked=" + IntToString(GetLocked(OBJECT_SELF)));
    PLogF(GetTag(OBJECT_SELF) + ".event.time", PSince() - GetLocalFloat(GetModule(), "T_" + GetTag(OBJECT_SELF)), 0.12);
}
