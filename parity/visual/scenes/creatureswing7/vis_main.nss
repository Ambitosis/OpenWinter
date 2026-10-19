// Four dire badgers rooted 2.9 m south of their dummies (out of close range), with all three creature weapons, the
// left claw, the right claw and the bite only: which ca1 clip each plays. See CSRun in cswing_inc.
#include "cswing_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    CSRun(pc, "nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|l;nw_s_badgerdire,,,|r;nw_s_badgerdire,,,|b", 2, "n2.9,n2.9,n2.9,n2.9");
}
