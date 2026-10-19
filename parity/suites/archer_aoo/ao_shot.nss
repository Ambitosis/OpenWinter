// F's OnPhysicalAttacked: an archer's shot.
#include "paoo_inc"
void main()
{
    object a = GetLocalObject(OBJECT_SELF, "A");
    if (GetLastAttacker() != a) return;
    AoMark(a, "shot" + IntToString(GetIsInCombat(OBJECT_SELF)) + IntToString(GetObjectSeen(a, OBJECT_SELF)));
    int k = GetLocalInt(a, "SN");
    SetLocalFloat(a, "S" + IntToString(k), PSince());
    SetLocalInt(a, "SN", k + 1);
}
