#include "mp_inc"
// mp_journaldm: the journal of a Dungeon Master (the DM client, nwmain -dmc) on nwserver: whether it has a World page
// (jnl_page_world) and its New button (jnl_wld_dm_new). Interactive: MP_NWN_INTERACTIVE=1 python
// tools/multiplayer/mp.py run mp_journaldm --only nwn --size 1280x720, driven from cmd.txt (DMA click
// 0.5,0.594 (Multiplayer), click 0.5,0.736 (Direct Connect), click 0.5,0.547, text dmpass, click 0.377,0.635 (OK),
// click 0.580,0.821 (Play), key 74 (journal), click 0.140,0.108 and 0.234,0.108 (its tabs)).

void main()
{
    object pc = GetEnteringObject();
    MPNote("enter|" + GetPCPlayerName(pc) + "|dm=" + IntToString(GetIsDM(pc)) + "|pc=" + IntToString(GetIsPC(pc)));
}
