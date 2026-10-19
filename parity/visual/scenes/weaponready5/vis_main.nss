// Ranged, dual wield, shield, unarmed: see WSRunSingle in parity/visual/include/weaponstance_inc.nss.
#include "weaponstance_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    WSRunSingle(pc, "nw_bandit001,nw_wbwxl001,,nw_wambo001;nw_bandit001,nw_wbwln001,,nw_wamar001;nw_bandit001,nw_wswss001,nw_wswdg001,;nw_bandit001,nw_wswls001,nw_ashsw001,");
}
