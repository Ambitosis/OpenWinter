// Logs each observer's perception events about its own subject, in order.
#include "parity_inc"
void main()
{
    object p = GetLastPerceived();
    if (p != GetLocalObject(OBJECT_SELF, "H")) return;
    string sKind = GetLastPerceptionSeen() ? "seen" : GetLastPerceptionHeard() ? "heard" : GetLastPerceptionVanished() ? "vanished" : GetLastPerceptionInaudible() ? "inaudible" : "none";
    string k = GetLocalString(OBJECT_SELF, "K");
    int n = GetLocalInt(OBJECT_SELF, "N") + 1;
    SetLocalInt(OBJECT_SELF, "N", n);
    PLog(k + ".ev" + IntToString(n), sKind);
}
