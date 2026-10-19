// OnPerception: every event after the test acts, as "<who><kind>@<seconds after the act>" (who: T the leaver,
// O the observer; kind: s seen, h heard, v vanished, i inaudible).
#include "parity_inc"
#include "parity_time"
void main()
{
    object me = OBJECT_SELF;
    float x = GetLocalFloat(me, "X");
    if (x <= 0.0) return;
    object p = GetLastPerceived();
    string w = p == GetLocalObject(me, "T") ? "T" : p == GetLocalObject(me, "O") ? "O" : "?";
    string k = GetLastPerceptionSeen() ? "s" : GetLastPerceptionHeard() ? "h" : GetLastPerceptionVanished() ? "v" : "i";
    SetLocalString(me, "EV", GetLocalString(me, "EV") + w + k + "@" + FloatToString(PSince() - x, 0, 2) + " ");
}
