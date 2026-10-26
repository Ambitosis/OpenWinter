// Signals another event from inside a handler and reads its own event again afterwards.
#include "pnwnxev_inc"
void main()
{
    EvLog("n1(" + NWNX_Events_GetCurrentEvent() + "," + NWNX_Events_GetEventData("K") + ")");
    NWNX_Events_PushEventData("K", "inner");
    int r = NWNX_Events_SignalEvent("EVT_A", OBJECT_SELF);
    EvLog("n2(" + IntToString(r) + "," + NWNX_Events_GetCurrentEvent() + "," + NWNX_Events_GetEventData("K") + ")");
    ExecuteScript("ev_b", OBJECT_SELF);
    DelayCommand(0.5, ExecuteScript("ev_b", GetModule()));
}
