// Maces and flails: see WSRunSingle in parity/visual/include/weaponstance_inc.nss.
#include "weaponstance_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    WSRunSingle(pc, "nw_bandit001,nw_wblml001,,;nw_bandit001,nw_wblms001,,;nw_bandit001,nw_wblfl001,,;nw_bandit001,nw_wswls001,,");
}
