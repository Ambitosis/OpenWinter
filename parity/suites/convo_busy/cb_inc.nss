// Conversation-busy probes: each case is a talker A<n> and a partner B<n>; every hook appends
// "<hook>:<self>/<pc speaker>/<last speaker>" to the module string "ev<n>".
#include "parity_inc"
string CbCase(object o) { return GetSubString(GetTag(o), 1, 10); }
void CbEvent(string sHook)
{
    string k = "ev" + CbCase(OBJECT_SELF);
    SetLocalString(GetModule(), k, GetLocalString(GetModule(), k) + " " + sHook + ":" + GetTag(OBJECT_SELF) + "/"
        + GetTag(GetPCSpeaker()) + "/" + GetTag(GetLastSpeaker()));
}
