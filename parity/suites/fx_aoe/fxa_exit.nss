// AoE OnExit.
#include "parity_inc"
void main()
{
    object o = GetExitingObject();
    string name = GetLocalString(OBJECT_SELF, "name");
    if (name == "carried") {
        float now = IntToFloat(GetTimeSecond() + 60 * GetTimeMinute()) + IntToFloat(GetTimeMillisecond()) / 1000.0;
        PLogF("aoe." + name + ".exit." + GetTag(o), now - GetLocalFloat(OBJECT_SELF, "born_at"), 0.15);
        WriteTimestampedLogEntry("AOE_DIAGNOSTIC|carried.phase|" + IntToString(GetLocalInt(GetModule(), "phase")));
    }
    else PLog("aoe." + name + ".exit." + GetTag(o), IntToString(GetLocalInt(GetModule(), "phase")));
}
