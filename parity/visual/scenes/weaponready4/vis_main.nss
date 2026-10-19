// Sling, throwing axes and small creatures: see WSRunSingle in parity/visual/include/weaponstance_inc.nss.
#include "weaponstance_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    WSRunSingle(pc, "nw_bandit001,nw_wbwsl001,,nw_wambu001;nw_bandit001,nw_wthax001,,50;nw_halfling001,nw_wblms001,,;nw_halfling001,nw_wblcl001,,");
}
