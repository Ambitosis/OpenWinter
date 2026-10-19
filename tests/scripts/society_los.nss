// Line of sight between module-local objects a, b and c.
void main()
{
    object m = GetModule();
    object a = GetLocalObject(m, "a");
    object b = GetLocalObject(m, "b");
    object c = GetLocalObject(m, "c");
    SetLocalInt(m, "ab", LineOfSightObject(a, b));
    SetLocalInt(m, "ac", LineOfSightObject(a, c));
    SetLocalInt(m, "ab_vec", LineOfSightVector(GetPosition(a), GetPosition(b)));
    SetLocalInt(m, "ac_vec", LineOfSightVector(GetPosition(a), GetPosition(c)));
}
