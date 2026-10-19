// Double weapons, trident and scythe: see WSRunSingle in parity/visual/include/weaponstance_inc.nss.
#include "weaponstance_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    WSRunSingle(pc, "nw_bandit001,nw_wdbsw001,,;nw_bandit001,nw_wdbma001,,;nw_bandit001,nw_wpltr001,,;nw_bandit001,nw_wplsc001,,");
}
