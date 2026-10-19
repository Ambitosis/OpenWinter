// One-handed weapons and unarmed: see parity/visual/include/weaponstance_inc.nss.
#include "weaponstance_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    WSRun(pc, "nw_bandit001,,,;nw_bandit001,nw_wswdg001,,;nw_bandit001,nw_wswss001,,;nw_bandit001,nw_wswls001,,;"
        + "nw_bandit001,nw_wblml001,,;nw_bandit001,nw_wblms001,,;nw_bandit001,nw_wblfl001,,;nw_bandit001,nw_wblhw001,,;"
        + "nw_bandit001,nw_wblcl001,,;nw_bandit001,nw_wswrp001,,;nw_bandit001,nw_waxhn001,,;nw_bandit001,x2_it_wpwhip,,");
}
