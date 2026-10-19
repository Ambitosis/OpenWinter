// Target OnDamaged: a blow landing on T1 ("d1") or T2 ("d2").
#include "rc_inc"
void main()
{
    object a = GetLastDamager();
    RcMark(a, GetLocalString(OBJECT_SELF, "K") == "s1" ? "d1" : "d2");
}
