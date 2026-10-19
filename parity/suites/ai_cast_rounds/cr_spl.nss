#include "parity_inc"
#include "parity_time"
void main()
{
    object c = GetLastSpellCaster();
    SetLocalString(c, "CR_SPL", GetLocalString(c, "CR_SPL") + IntToString(GetLastSpell()) + "@" + FloatToString(PSince() - GetLocalFloat(c, "CR_T0"), 0, 1) + " ");
}
