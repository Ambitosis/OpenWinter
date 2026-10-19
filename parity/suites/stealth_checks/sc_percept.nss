// Records when an observer first sees / hears its own hider (seconds since the observer was made).
#include "parity_time"
void main()
{
    object p = GetLastPerceived();
    if (p != GetLocalObject(OBJECT_SELF, "H")) return;
    float t = PSince() - GetLocalFloat(OBJECT_SELF, "BORN");
    if (GetLastPerceptionSeen() && GetLocalFloat(OBJECT_SELF, "SEEN") == 0.0) SetLocalFloat(OBJECT_SELF, "SEEN", t);
    if (GetLastPerceptionHeard() && GetLocalFloat(OBJECT_SELF, "HEARD") == 0.0) SetLocalFloat(OBJECT_SELF, "HEARD", t);
}
