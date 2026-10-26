// Logs the current NWNX event, OBJECT_SELF and the data tags the suite asked for; skips or sets a result on request.
#include "pnwnxev_inc"
void main()
{
    string e = NWNX_Events_GetCurrentEvent();
    object oMod = GetModule();
    EvLog(EvShort(e) + "@" + EvObj(OBJECT_SELF) + EvData(GetLocalString(oMod, "EVTAGS:" + e)));
    if (GetLocalInt(oMod, "EVSKIP:" + e)) NWNX_Events_SkipEvent();
    string r = GetLocalString(oMod, "EVRESULT:" + e);
    if (r != "") NWNX_Events_SetEventResult(r);
}
