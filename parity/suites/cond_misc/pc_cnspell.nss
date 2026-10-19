// Target OnSpellCastAt: counts the spell for the caster's group.
#include "pcond_inc"
void main()
{
    object a = GetLastSpellCaster();
    CAdd(GetLocalString(a, "G") + ".cast." + IntToString(GetLastSpell()));
}
