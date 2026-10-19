// Attacker OnEndCombatRound, then the stock end-of-round AI.
#include "rc_inc"
void main() { RcMark(OBJECT_SELF, "e"); ExecuteScript("nw_c2_default3", OBJECT_SELF); }
