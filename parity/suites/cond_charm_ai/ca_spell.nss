// OnSpellCastAt on a bystander: the level of the spell that reached it (spells of one level are picked among at
// random on nwserver), from whom, when.
#include "parity_time"
void main()
{
    object m = GetModule();
    object c = GetLastSpellCaster();
    string k = GetLocalString(c, "G");
    string s = GetLocalString(m, k + ".log") + " at" + GetLocalString(OBJECT_SELF, "R") + ":" + "L" + Get2DAString("spells", "Innate", GetLastSpell()) + "@" + FloatToString(PNow() - GetLocalFloat(c, "T0"), 0, 1);
    SetLocalString(m, k + ".log", s);
}
