// Four stock dire badgers rooted (EffectCutsceneImmobilize) with their dummies 1.0, 1.6, 2.2 and 2.8 m away:
// whether the distance picks ccloseh or the ca1 clips. See CSRun in cswing_inc.
#include "cswing_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    CSRun(pc, "nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|*", TRUE, "1.0,1.6,2.2,2.8");
}
