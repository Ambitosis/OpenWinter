// OnHeartbeat of a beat-timed creature (local HB = its group): records the interval since its previous beat.
#include "parity_time"
void main()
{
    object me = OBJECT_SELF;
    string g = GetLocalString(me, "HB");
    if (g == "") return;
    float t = PSince(), last = GetLocalFloat(me, "LAST");
    SetLocalFloat(me, "LAST", t);
    if (last <= 0.0) return;
    object m = GetModule();
    float d = t - last;
    SetLocalInt(m, g + "N", GetLocalInt(m, g + "N") + 1);
    SetLocalFloat(m, g + "SUM", GetLocalFloat(m, g + "SUM") + d);
    if (GetLocalFloat(m, g + "MIN") == 0.0 || d < GetLocalFloat(m, g + "MIN")) SetLocalFloat(m, g + "MIN", d);
    if (d > GetLocalFloat(m, g + "MAX")) SetLocalFloat(m, g + "MAX", d);
    SetLocalString(m, g + "RAW", GetLocalString(m, g + "RAW") + " " + FloatToString(d, 0, 2));
}
