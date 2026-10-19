#include "mp_inc"
// mp_portal: the prompt ActivatePortal shows a player on a server (ingame_portal; single player shows nothing).
// Original: python tools/multiplayer/mp.py run mp_portal --only nwn --size 1280x720; OpenWinter: the same without
// --only (add --client-args "--ui classic" for the classic prompt). The addresses are a local port nothing listens on
// and the harness's own server (port 5951, scene.json), so nothing real is ever joined. VISUAL|click / key are input
// for the original client (Cancel and Join at 1280x720), MPDo portal|... the same buttons on OpenWinter.
// Joining leaves and comes back, so the stage is kept in a campaign database (the server's sandbox): 0 the prompt,
// 1 after Join (arrived at the waypoint?), an empty address, a seamless portal; 2 after the seamless portal.

const string CANCEL = "0.407,0.632";
const string JOIN = "0.591,0.633";
const string NOBODY = "127.0.0.1:5199";
const string SELF = "127.0.0.1:5951";

void In(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }

// A prompt button: a real-pointer click on the original, the prompt's own button on OpenWinter (MPDo portal|...).
void Press(string sButton, string sAt)
{
    In("click|MPA|" + sAt);
    MPDo("MPA", "portal|" + sButton);
}

void State(string sStep)
{
    object pc = MPPlayer("MPA");
    MPNote("state|" + sStep + "|pc=" + IntToString(GetIsObjectValid(pc)) + "|commandable=" + IntToString(GetCommandable(pc))
        + "|pos=" + MPPos(pc) + "|action=" + IntToString(GetCurrentAction(pc)));
}

void Portal(string sStep, string sAddress, string sPassword, string sWaypoint, int bSeamless)
{
    object pc = MPPlayer("MPA");
    MPNote("portal|" + sStep + "|" + sAddress + "|" + sPassword + "|" + sWaypoint + "|" + IntToString(bSeamless));
    SendMessageToPC(pc, "PORTAL " + sStep);
    ActivatePortal(pc, sAddress, sPassword, sWaypoint, bSeamless);
    DelayCommand(0.5, State(sStep));
}

void Stage(int n) { SetCampaignInt("mp_portal", "stage", n); }

void Run0()
{
    float t = 0.0;
    // An address nobody answers: the check, and what happens when it gives up.
    DelayCommand(t += 1.0, Portal("a", NOBODY, "secret", "WP_PORTAL", FALSE));
    DelayCommand(t += 2.0, MPShot("a_02"));
    DelayCommand(t += 10.0, MPShot("a_12"));
    DelayCommand(t += 0.5, State("a_12"));
    // An empty address while the prompt is open.
    DelayCommand(t += 1.5, Portal("b", "", "", "", FALSE));
    DelayCommand(t += 2.0, MPShot("b_empty"));
    // Another address while it is open, then Cancel.
    DelayCommand(t += 1.0, Portal("c", NOBODY, "", "", FALSE));
    DelayCommand(t += 1.0, MPShot("c_again"));
    DelayCommand(t += 1.0, Press("cancel", CANCEL));
    DelayCommand(t += 2.0, MPShot("c_cancel"));
    // Esc.
    DelayCommand(t += 1.0, Portal("d", NOBODY, "", "", FALSE));
    DelayCommand(t += 2.0, In("key|MPA|27"));
    DelayCommand(t += 2.0, MPShot("d_esc"));
    DelayCommand(t += 1.0, Press("cancel", CANCEL));
    DelayCommand(t += 2.0, MPShot("d_esc_cancel"));
    // The harness's own server: what it shows of a server that answers, then Join (to the waypoint).
    DelayCommand(t += 1.0, Portal("e", SELF, "", "WP_PORTAL", FALSE));
    DelayCommand(t += 1.0, MPShot("e_01"));
    DelayCommand(t += 3.0, MPShot("e_04"));
    DelayCommand(t += 1.0, Stage(1));
    DelayCommand(t += 0.5, Press("join", JOIN));
    DelayCommand(t += 1.0, MPShot("e_join_01"));
    DelayCommand(t += 3.0, MPShot("e_join_04"));
}

void Run1()
{
    float t = 0.0;
    DelayCommand(t += 1.0, State("arrived"));
    DelayCommand(t += 1.0, MPShot("f_arrived"));
    // An empty address with no prompt open (nw_defportal's call).
    DelayCommand(t += 1.0, Portal("f", "", "", "", FALSE));
    DelayCommand(t += 2.0, MPShot("f_empty"));
    DelayCommand(t += 10.0, MPShot("f_empty_12"));
    DelayCommand(t += 1.0, Press("cancel", CANCEL));
    DelayCommand(t += 2.0, MPShot("f_cancel"));
    // A seamless portal back here, to the waypoint: no prompt.
    DelayCommand(t += 1.0, Stage(2));
    DelayCommand(t += 0.5, Portal("g", SELF, "", "WP_PORTAL", TRUE));
    DelayCommand(t += 0.5, MPShot("g_00"));
    DelayCommand(t += 1.5, MPShot("g_02"));
    DelayCommand(t += 3.0, MPShot("g_05"));
}

void Run2()
{
    float t = 0.0;
    DelayCommand(t += 1.0, State("arrived_seamless"));
    DelayCommand(t += 1.0, MPShot("h_arrived"));
    DelayCommand(t += 1.0, MPDone());
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    int stage = GetCampaignInt("mp_portal", "stage");
    MPNote("enter|" + GetPCPlayerName(pc) + "|" + MPPos(pc) + "|stage=" + IntToString(stage)
        + "|waypoint=" + MPPos(GetObjectByTag("WP_PORTAL")));
    if (GetLocalInt(GetModule(), "portal_stage_run") == stage + 1) return;
    SetLocalInt(GetModule(), "portal_stage_run", stage + 1);
    SetPlotFlag(pc, TRUE);
    DelayCommand(1.0, MPCameras(90.0, 10.0, 55.0));
    if (stage == 0) DelayCommand(5.0, Run0());
    else if (stage == 1) DelayCommand(5.0, Run1());
    else DelayCommand(5.0, Run2());
}
