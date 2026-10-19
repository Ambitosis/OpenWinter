// Four stock dire badgers rooted (EffectCutsceneImmobilize) with their dummies 2.3, 2.45, 2.6 and 2.7 m away:
// whether the distance picks ccloseh or the ca1 clips. See CSRun in cswing_inc.
#include "cswing_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    CSRun(pc, "nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|*", TRUE, "2.3,2.45,2.6,2.7");
}
