#include "pnwnxev_inc"
void main()
{
    EvLog("b(" + NWNX_Events_GetCurrentEvent() + "," + NWNX_Events_GetEventData("K") + ")");
}
