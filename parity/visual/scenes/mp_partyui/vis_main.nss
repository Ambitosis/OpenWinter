#include "mp_inc"
// mp_partyui: one original client on nwserver (tools/multiplayer/mp.py run mp_partyui --only nwn with
// MP_NWN_INTERACTIVE=1, driven from cmd.txt): the classic Player List window (party bar button, ingame_pvp.gui) with
// only one player on the server, and the self radial in a multiplayer game. Scripts log what they see of the party.

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    MPNote("enter|leader=" + GetName(GetFactionLeader(pc)) + "|first=" + GetName(GetFirstFactionMember(pc, TRUE))
        + "|avgLevel=" + IntToString(GetFactionAverageLevel(pc)));
    DelayCommand(1.0, MPCameras(90.0, 10.0, 55.0));
}
