#include "parity_inc"
#include "parity_time"

void main()
{
    object p = GetLastPerceived();
    string sKind = GetLastPerceptionSeen() ? "seen" : GetLastPerceptionHeard() ? "heard" : GetLastPerceptionVanished() ? "vanished" : GetLastPerceptionInaudible() ? "inaudible" : "none";
    if (GetLocalInt(GetModule(), "PARITY_STOP")) return;
    string k = "event." + GetTag(OBJECT_SELF);
    // Per observer and target (the order between targets follows the original's area lists).
    PLog(k + "." + GetTag(p), sKind);
    // Perception updates every 4 s from when the observer appeared; the original staggers creatures made
    // together by up to half a second, so the walker (1.75 m/s) may be up to a metre further on.
    if (GetTag(p) == "WALKER") PLogF(k + "." + sKind + ".dist", GetDistanceBetween(OBJECT_SELF, p), 1.0);
    if (GetTag(p) == "JUMPER") PLogF(k + "." + sKind + ".time", PSince() - GetLocalFloat(OBJECT_SELF, "BORN"), 0.35);
}
