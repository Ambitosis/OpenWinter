// OnPerception: counts each kind of perception event.
void main()
{
    object o = OBJECT_SELF;
    SetLocalObject(o, "perceived", GetLastPerceived());
    if (GetLastPerceptionSeen()) SetLocalInt(o, "seen", GetLocalInt(o, "seen") + 1);
    if (GetLastPerceptionHeard()) SetLocalInt(o, "heard", GetLocalInt(o, "heard") + 1);
    if (GetLastPerceptionVanished()) SetLocalInt(o, "vanished", GetLocalInt(o, "vanished") + 1);
    if (GetLastPerceptionInaudible()) SetLocalInt(o, "inaudible", GetLocalInt(o, "inaudible") + 1);
    SetLocalInt(o, "objseen", GetObjectSeen(GetLastPerceived()));
    SetLocalInt(o, "objheard", GetObjectHeard(GetLastPerceived()));
}
