// The archer's OnPhysicalAttacked: F attacks it ("F" plus F's current action).
#include "paoo_inc"
void main()
{
    object a = OBJECT_SELF;
    object f = GetLocalObject(a, "F");
    if (GetLastAttacker() != f) return;
    AoMark(a, "F" + IntToString(GetCurrentAction(f)));
    if (GetLocalFloat(a, "H0") <= 0.0)
    {
        SetLocalFloat(a, "H0", PSince());
        SetLocalFloat(a, "HD0", GetDistanceBetween(a, f));
    }
}
