// Staves, spear, heavy flail and whip: see WSRunSingle in parity/visual/include/weaponstance_inc.nss.
#include "weaponstance_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    WSRunSingle(pc, "nw_bandit001,nw_wmgst002,,;nw_bandit001,nw_wplss001,,;nw_bandit001,nw_wblfh001,,;nw_bandit001,x2_it_wpwhip,,");
}
