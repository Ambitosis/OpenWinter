#include "ew_inc"
// Files a spawned creature under the running trial and keeps it still (no scripts, so it doesn't move into a square).
void main()
{
    string sLabel = GetLocalString(Mod(), "cur");
    int n = GetLocalInt(Mod(), "n_" + sLabel) + 1;
    SetLocalInt(Mod(), "n_" + sLabel, n);
    SetLocalObject(Mod(), "o_" + sLabel + "_" + IntToString(n), OBJECT_SELF);
    Quiet(OBJECT_SELF);
}
