// Runs on the attacker at its second swing at T1 ("x" marks when the test acts).
#include "rc_inc"
void Act(object a)
{
    RcMark(a, "x");
    if (GetLocalInt(a, "MODE") == 1)
    {
        AssignCommand(a, ClearAllActions());
        AssignCommand(a, ActionAttack(GetLocalObject(a, "T2")));
    }
    else AssignCommand(a, ClearAllActions(TRUE));
}
void main()
{
    object a = OBJECT_SELF;
    DelayCommand(GetLocalFloat(a, "DELAY"), Act(a));
}
