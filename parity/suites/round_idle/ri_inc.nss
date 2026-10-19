// round_idle: per-attacker event times (seconds since PStart) kept on the attacker as "<kind><n>" floats, and a
// readable sequence "SEQ".
#include "parity_inc"
#include "parity_time"

void RiMark(object a, string sKind)
{
    int n = GetLocalInt(a, sKind + "n");
    SetLocalFloat(a, sKind + IntToString(n), PSince());
    SetLocalInt(a, sKind + "n", n + 1);
    SetLocalString(a, "SEQ", GetLocalString(a, "SEQ") + sKind + "@" + FloatToString(PSince(), 0, 2) + " ");
}

void RiNote(object a, string s)
{
    SetLocalString(a, "SEQ", GetLocalString(a, "SEQ") + "[" + s + "] ");
}
