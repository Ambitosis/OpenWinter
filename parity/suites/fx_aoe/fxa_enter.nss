// AoE OnEnter: who entered, when, and what the AoE reports about itself.
#include "parity_inc"
void main()
{
    object o = GetEnteringObject();
    float t = IntToFloat(GetTimeSecond() + 60 * GetTimeMinute()) + IntToFloat(GetTimeMillisecond()) / 1000.0;
    if (!GetLocalInt(OBJECT_SELF, "clock_set")) { SetLocalInt(OBJECT_SELF, "clock_set", TRUE); SetLocalFloat(OBJECT_SELF, "born_at", t); }
    PLog("aoe." + GetLocalString(OBJECT_SELF, "name") + ".enter." + GetTag(o), "creator=" + GetTag(GetAreaOfEffectCreator()) + " self=" + GetTag(OBJECT_SELF)
        + " type=" + IntToString(GetObjectType(OBJECT_SELF)) + " res=" + GetResRef(OBJECT_SELF));
}
