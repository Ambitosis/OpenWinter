// An area of effect's OnEnter: is the entering creature already in its sub-area list?
void main()
{
    object o = GetEnteringObject(), m = GetModule();
    string sK = GetLocalString(OBJECT_SELF, "K");
    // Events that come before the suite tags the fog: named by where it lies.
    vector c = GetPosition(OBJECT_SELF);
    if (sK == "") sK = c.z > 2.0 ? (c.y > 55.0 ? "UP2" : "UP") : c.x > 40.0 ? "LATE" : c.y > 55.0 ? "FLAT2" : "FLAT";
    string k = sK + "." + GetTag(o);
    string s = GetLocalString(m, "EV_" + k);
    SetLocalString(m, "EV_" + k, s + "enter" + IntToString(GetIsInSubArea(o, OBJECT_SELF)) + " ");
    vector a = GetPosition(OBJECT_SELF), p = GetPosition(o);
    if (GetLocalFloat(m, "D_" + k) == 0.0) SetLocalFloat(m, "D_" + k, VectorMagnitude(Vector(p.x - a.x, p.y - a.y, 0.0)));
}
