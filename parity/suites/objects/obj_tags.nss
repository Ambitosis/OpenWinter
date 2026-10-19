// Tags scripts set keep only letters, digits and underscores (waypoints created with a tag keep it as given).
#include "parity_inc"
void T(string k, object o)
{
    PLog("tag." + k + ".create", GetTag(o));
    SetTag(o, "x.y.z");
    PLog("tag." + k + ".set", GetTag(o));
    SetTag(o, ".a b,c;d:e/f\\g'h\"i-j_k!l?m");
    PLog("tag." + k + ".set.punct", GetTag(o));
    PLogI("tag." + k + ".bytag.dotted", GetIsObjectValid(GetObjectByTag("x.y.z")));
}
void Go()
{
    T("wp", CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(), FALSE, "w.1"));
    T("plc", CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(1.0, 0.0), FALSE, "p.1"));
    T("cre", CreateObject(OBJECT_TYPE_CREATURE, "nw_dog", PCentre(3.0, 0.0), FALSE, "c.1"));
    T("item", CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre(0.0, 2.0), FALSE, "i.1"));
    T("store", CreateObject(OBJECT_TYPE_STORE, "nw_storebar01", PCentre(0.0, -2.0), FALSE, "s.1"));
    T("area", CreateArea("parity", "TAGAREA"));
    object a2 = CreateArea("parity", "A.2", "N.2");
    PLog("tag.area.created", GetTag(a2));
    object c = CopyObject(GetObjectByTag("abcdefghij_klm"), PCentre(-2.0, 0.0), OBJECT_INVALID, "cp.1");
    PLog("tag.copy", GetTag(c));
    
}
void main() { Go(); }
