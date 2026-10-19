// Engine-structure constants in default parameters: LOCATION_INVALID and the JSON_* literals.
#include "parity_inc"
void F(json j = JSON_OBJECT, location l = LOCATION_INVALID, json k = JSON_ARRAY, json t = JSON_TRUE, json f = JSON_FALSE, json n = JSON_NULL, json s = JSON_STRING)
{
    PLog("const.j", JsonDump(j));
    PLogI("const.l.valid", GetIsObjectValid(GetAreaFromLocation(l)));
    PLog("const.k", JsonDump(k));
    PLog("const.t", JsonDump(t));
    PLog("const.f", JsonDump(f));
    PLog("const.n", JsonDump(n));
    PLog("const.s", JsonDump(s));
}
void Go()
{
    F();
    location l = LOCATION_INVALID;
    PLogI("const.l2.valid", GetIsObjectValid(GetAreaFromLocation(l)));
    PLog("const.after", "ok");
}
void main() { Go(); }
