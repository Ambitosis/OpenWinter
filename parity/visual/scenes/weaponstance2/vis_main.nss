// Two-handed, polearm, staff and double weapons: see parity/visual/include/weaponstance_inc.nss.
#include "weaponstance_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    WSRun(pc, "nw_bandit001,nw_wswgs001,,;nw_bandit001,nw_waxgr001,,;nw_bandit001,nw_wblfh001,,;nw_bandit001,nw_wplhb001,,;"
        + "nw_bandit001,nw_wdbqs001,,;nw_bandit001,nw_wplss001,,;nw_bandit001,nw_wplsc001,,;nw_bandit001,nw_wmgst002,,;"
        + "nw_bandit001,nw_wdbsw001,,;nw_bandit001,nw_wdbma001,,;nw_bandit001,nw_wpltr001,,;nw_bandit001,nw_wswbs001,,");
}
