// Four stock Summon Creature I dire badgers (claws and bite), their distances logged: which of ca1* and ccloseh
// each plays, with five extra attacks a round and then without. See CSRun in cswing_inc.
#include "cswing_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    CSRun(pc, "nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|*;nw_s_badgerdire,,,|*", TRUE);
}
