#include "pnwnxev_inc"
void main()
{
    EvLog("a(" + NWNX_Events_GetCurrentEvent() + "," + NWNX_Events_GetEventData("K") + "," + NWNX_Events_GetEventData("MISSING") + "," + EvObj(OBJECT_SELF) + ")");
}
