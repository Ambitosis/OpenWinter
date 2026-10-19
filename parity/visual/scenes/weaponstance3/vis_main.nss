// Shields, dual wield, ranged and a small creature: see parity/visual/include/weaponstance_inc.nss.
#include "weaponstance_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    WSRun(pc, "nw_bandit001,nw_wswls001,nw_ashsw001,;nw_bandit001,nw_wblml001,nw_ashto001,;nw_bandit001,nw_wswss001,nw_wswdg001,;nw_bandit001,nw_wbwln001,,nw_wamar001;"
        + "nw_bandit001,nw_wbwsh001,,nw_wamar001;nw_bandit001,nw_wbwxl001,,nw_wambo001;nw_bandit001,nw_wbwxh001,,nw_wambo001;nw_bandit001,nw_wbwsl001,,nw_wambu001;"
        + "nw_bandit001,nw_wthax001,,50;nw_halfling001,nw_wblcl001,,;nw_halfling001,nw_wswss001,,;nw_halfling001,nw_wblms001,,");
}
