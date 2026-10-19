// AoE heartbeat: counts, and the objects inside.
#include "parity_inc"
void main()
{
    int n = GetLocalInt(OBJECT_SELF, "beats") + 1;
    SetLocalInt(OBJECT_SELF, "beats", n);
    string s = "";
    object o = GetFirstInPersistentObject();
    while (GetIsObjectValid(o)) { s += GetTag(o) + " "; o = GetNextInPersistentObject(); }
    PLog("aoe." + GetLocalString(OBJECT_SELF, "name") + ".beat" + IntToString(n), "phase=" + IntToString(GetLocalInt(GetModule(), "phase")) + " inside=" + s);
}
