// Logs when an event fires, in seconds since the step began (PStart).
#include "pnwnxev_inc"
#include "parity_time"
void main()
{
    object oMod = GetModule();
    string e = EvShort(NWNX_Events_GetCurrentEvent());
    int n = GetLocalInt(oMod, "T:" + e);
    SetLocalInt(oMod, "T:" + e, n + 1);
    PLogF("t." + GetLocalString(oMod, "STEP") + "." + e + "." + IntToString(n), PSince(), 0.3);
}
