#include "eb_inc"
// Files a spawned creature under the running walk-in and its encounter (by tag EB_k).
void main()
{
    string sLabel = GetLocalString(Mod(), "cur");
    int n = GetLocalInt(Mod(), "n_" + sLabel) + 1;
    SetLocalInt(Mod(), "n_" + sLabel, n);
    SetLocalObject(Mod(), "o_" + sLabel + "_" + IntToString(n), OBJECT_SELF);
    string k = GetStringRight(GetTag(OBJECT_SELF), 1);
    SetLocalInt(Mod(), "c_" + sLabel + "_" + k, GetLocalInt(Mod(), "c_" + sLabel + "_" + k) + 1);
    if (GetStringLeft(sLabel, 1) == "k") Log("spawn|" + sLabel + "|" + IntToString(n));
}
