// A summoned dire badger playing no attack clip: Summon Creature I's dire badger with all three
// creature weapons, the left claw only, the right claw only and the bite only. See CSRun in cswing_inc.
#include "cswing_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    CSRun(pc, "nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|l;nw_s_badgerdire,,,|r;nw_s_badgerdire,,,|b");
}
