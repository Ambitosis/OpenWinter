// Target OnPhysicalAttacked: a swing at T1 ("s1") or T2 ("s2"); the second swing at T1 starts the group's test.
#include "rc_inc"
void main()
{
    object a = GetLastAttacker();
    string k = GetLocalString(OBJECT_SELF, "K");
    RcMark(a, k);
    if (k == "s1" && GetLocalInt(a, "s1n") == 2) ExecuteScript("rc_test", a);
}
