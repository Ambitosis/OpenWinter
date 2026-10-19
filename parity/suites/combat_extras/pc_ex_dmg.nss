// Death attack target OnDamaged: counts hits; the attacker stops after the first.
#include "pcombat_inc"
void main()
{
    object a = GetLastDamager();
    PAdd(GetLocalString(OBJECT_SELF, "G") + ".hit");
    AssignCommand(a, ClearAllActions(TRUE));
}
