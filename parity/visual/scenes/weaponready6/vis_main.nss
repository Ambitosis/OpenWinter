// Two-handers, quarterstaff and unarmed: see WSRunSingle in parity/visual/include/weaponstance_inc.nss.
#include "weaponstance_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    WSRunSingle(pc, "nw_bandit001,nw_wswgs001,,;nw_bandit001,nw_waxgr001,,;nw_bandit001,nw_wdbqs001,,;nw_bandit001,,,");
}
