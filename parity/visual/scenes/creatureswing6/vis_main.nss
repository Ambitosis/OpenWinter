// Four wolves (bite only, PREFATCKDIST 2.3) rooted (EffectCutsceneImmobilize) with their dummies 1.4, 1.8, 2.1 and 2.5 m away:
// whether the distance picks ccloseh or the ca1 clips. See CSRun in cswing_inc.
#include "cswing_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    CSRun(pc, "nw_wolf,,,|*;nw_wolf,,,|*;nw_wolf,,,|*;nw_wolf,,,|*", TRUE, "1.4,1.8,2.1,2.5");
}
