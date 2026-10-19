#include "parity_inc"
#include "parity_time"

// Heartbeats around a clock jump: does a jump make heartbeats come due at once?
void main()
{
    if (!GetLocalInt(GetModule(), "WATCH_HB")) return;
    object o = OBJECT_SELF;
    string k = o == GetModule() ? "module" : GetTag(o);
    PLogF("hb." + k, PSince() - GetLocalFloat(GetModule(), "T_JUMP"), 0.35);
}
