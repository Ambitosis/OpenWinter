// Logs the current NWNX event, OBJECT_SELF and the data tags the suite asked for; skips or sets a result on request.
#include "pnwnxev_inc"
void main()
{
    string e = NWNX_Events_GetCurrentEvent();
    object oMod = GetModule();
    // EVMAX: log only the first few of a repeating event.
    int nMax = GetLocalInt(oMod, "EVMAX:" + e);
    int nSeen = GetLocalInt(oMod, "EVSEEN:" + e) + 1;
    SetLocalInt(oMod, "EVSEEN:" + e, nSeen);
    if (nMax > 0 && nSeen > nMax) return;
    EvLog(EvShort(e) + "@" + EvObj(OBJECT_SELF) + EvData(GetLocalString(oMod, "EVTAGS:" + e)));
    if (GetLocalInt(oMod, "EVSKIP:" + e)) NWNX_Events_SkipEvent();
    string r = GetLocalString(oMod, "EVRESULT:" + e);
    if (r != "") NWNX_Events_SetEventResult(r);
}
