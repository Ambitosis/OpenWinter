// Target OnPhysicalAttacked: a swing ("s"); the second swing starts the attacker's test (modes 1-3).
#include "ri_inc"
void Act(object a)
{
    RiMark(a, "x");
    int m = GetLocalInt(a, "MODE");
    if (m == 2) AssignCommand(a, ClearAllActions());
    else AssignCommand(a, ClearAllActions(TRUE));
}
void main()
{
    object a = GetLastAttacker();
    RiMark(a, "s");
    if (GetLocalInt(a, "sn") == 1) SetLocalInt(a, "AIL", GetAILevel(a));
    if (GetLocalInt(a, "MODE") > 0 && GetLocalInt(a, "sn") == 2) DelayCommand(0.2, Act(a));
}
