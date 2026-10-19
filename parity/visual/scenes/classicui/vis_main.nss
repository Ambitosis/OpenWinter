// The original interface for the Classic UI mode: the in-game HUD alone, then each window opened by its
// key (posted to the window, VISUAL|key|<vk>|tap), screenshotted and closed again. Not in cutscene mode,
// so the GUI shows. OpenWinter's side needs --ui classic to compare.
#include "visual_inc"

void Tap(int nVk)
{
    WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap");
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetPlotFlag(oPC, TRUE);
    VSpawn(oPC, "nw_bandit001", 0.0, 3.0, 270.0);
    DelayCommand(2.0, VCamera(oPC, 90.0, 8.0, 60.0));
    DelayCommand(5.0, VShot("a_hud"));
    // I, C, B, J, M: inventory, character, spellbook, journal, map; Esc: the game menu.
    DelayCommand(7.0, Tap(73));
    DelayCommand(9.0, VShot("b_inventory"));
    DelayCommand(10.0, Tap(73));
    DelayCommand(11.0, Tap(67));
    DelayCommand(13.0, VShot("c_character"));
    DelayCommand(14.0, Tap(67));
    DelayCommand(15.0, Tap(66));
    DelayCommand(17.0, VShot("d_spellbook"));
    DelayCommand(18.0, Tap(66));
    DelayCommand(19.0, Tap(74));
    DelayCommand(21.0, VShot("e_journal"));
    DelayCommand(22.0, Tap(74));
    DelayCommand(23.0, Tap(77));
    DelayCommand(25.0, VShot("f_map"));
    DelayCommand(26.0, Tap(77));
    // Everything at once, as players often have it.
    DelayCommand(27.0, Tap(73));
    DelayCommand(27.5, Tap(67));
    DelayCommand(29.0, VShot("h_inv_char"));
    DelayCommand(30.0, Tap(73));
    DelayCommand(30.5, Tap(67));
    // The game menu pauses single player (and this scene's timers): the harness shoots it on its own clock.
    DelayCommand(32.0, WriteTimestampedLogEntry("VISUAL|keyshot|27|g_menu|1.5|27"));
    DelayCommand(37.0, VDone());
}
