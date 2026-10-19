// Other creature models: a badger (claws and bite), a wolf (bite only), a giant spider (three creature weapons)
// and a dire badger without creature weapons. See CSRun in cswing_inc.
#include "cswing_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    CSRun(pc, "nw_badger,,,|*;nw_wolf,,,|*;nw_spidgiant,,,|*;nw_s_badgerdire,,,|");
}
