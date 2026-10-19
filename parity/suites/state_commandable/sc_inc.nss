// state_commandable: every statescripts.2da row 1-9 runs sc_st<row>, which records the row, the time since the
// condition landed and GetCommandable inside the state script ("<row>c<0|1>@<t>").
#include "parity_inc"
#include "parity_time"

void ScMark(object o, string sTok)
{
    float t0 = GetLocalFloat(o, "T0");
    SetLocalString(o, "SEQ", GetLocalString(o, "SEQ") + sTok + "@" + FloatToString(PNow() - t0, 0, 2) + " ");
}

void ScState(string sRow)
{
    object o = OBJECT_SELF;
    if (GetLocalFloat(o, "T0") <= 0.0) return;
    ScMark(o, sRow + "c" + IntToString(GetCommandable(o)));
    string g = GetLocalString(o, "G");
    object m = GetModule();
    SetLocalInt(m, g + ".runs", GetLocalInt(m, g + ".runs") + 1);
    SetLocalInt(m, g + ".incmd", GetLocalInt(m, g + ".incmd") + GetCommandable(o));
}
