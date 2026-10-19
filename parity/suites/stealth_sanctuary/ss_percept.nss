// Counts each observer's perception events about its own subject (per group, by kind).
#include "parity_inc"
void main()
{
    object p = GetLastPerceived();
    if (p != GetLocalObject(OBJECT_SELF, "H")) return;
    string sKind = GetLastPerceptionSeen() ? "seen" : GetLastPerceptionHeard() ? "heard" : GetLastPerceptionVanished() ? "vanished" : GetLastPerceptionInaudible() ? "inaudible" : "none";
    string k = GetLocalString(OBJECT_SELF, "G") + ".ev." + sKind;
    SetLocalInt(GetModule(), k, GetLocalInt(GetModule(), k) + 1);
}
