#include "parity_inc"
#include "parity_time"
void main()
{
    object o = OBJECT_SELF;
    object c = GetLastSpellCaster();
    string k = "TU_LOG";
    SetLocalString(c, k, GetLocalString(c, k) + IntToString(GetLastSpell()) + (o == c ? "s" : "t") + (GetLastSpellHarmful() ? "!" : "") + "@" + FloatToString(PSince() - GetLocalFloat(c, "TU_T0"), 0, 1) + " ");
}
