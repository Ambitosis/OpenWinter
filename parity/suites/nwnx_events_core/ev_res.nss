// Reads skip and result state across subscribers of one effect event.
#include "pnwnxev_inc"
void main()
{
    string e = NWNX_Events_GetCurrentEvent();
    EvLog("r(" + EvShort(e) + ")");
    NWNX_Events_SetEventResult("first");
}
