// State-script logging (suite state_scripts): every statescripts.2da row 1-9 runs ss_st<row>, which records the row
// and the time on the creature; ss_hb records an OnHeartbeat the same way.
#include "parity_inc"
#include "parity_time"

void SsLog(string sTok)
{
    object o = OBJECT_SELF;
    float t = PNow();
    float t0 = GetLocalFloat(o, "T0");
    // Before the condition lands, only heartbeats are kept (as "h-").
    if (t0 <= 0.0) { if (sTok == "h") SetLocalInt(o, "HB0", GetLocalInt(o, "HB0") + 1); return; }
    SetLocalString(o, "SEQ", GetLocalString(o, "SEQ") + sTok + "@" + FloatToString(t - t0, 0, 2) + " ");
    if (sTok == "h") return;
    string g = GetLocalString(o, "G");
    object m = GetModule();
    SetLocalInt(m, g + ".runs", GetLocalInt(m, g + ".runs") + 1);
    SetLocalInt(m, g + ".row" + sTok, GetLocalInt(m, g + ".row" + sTok) + 1);
    if (GetLocalInt(o, "N") == 0)
    {
        SetLocalFloat(m, g + ".first", GetLocalFloat(m, g + ".first") + (t - t0));
        SetLocalInt(m, g + ".firstn", GetLocalInt(m, g + ".firstn") + 1);
        float mx = t - t0;
        if (mx > GetLocalFloat(m, g + ".firstmax")) SetLocalFloat(m, g + ".firstmax", mx);
    }
    else
    {
        float gap = t - GetLocalFloat(o, "LAST");
        SetLocalFloat(m, g + ".gap", GetLocalFloat(m, g + ".gap") + gap);
        SetLocalInt(m, g + ".gapn", GetLocalInt(m, g + ".gapn") + 1);
        if (gap > GetLocalFloat(m, g + ".gapmax")) SetLocalFloat(m, g + ".gapmax", gap);
        if (GetLocalFloat(m, g + ".gapmin") == 0.0 || gap < GetLocalFloat(m, g + ".gapmin")) SetLocalFloat(m, g + ".gapmin", gap);
    }
    SetLocalInt(o, "N", GetLocalInt(o, "N") + 1);
    SetLocalFloat(o, "LAST", t);
}
