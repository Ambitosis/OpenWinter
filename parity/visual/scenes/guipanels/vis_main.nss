#include "visual_inc"
// SetGuiPanelDisabled and the built-in GUI events on the original (run with --only nwn; vis_gui logs each event as
// a note). Each panel is opened by its key, disabled while open, its key pressed again, forced open with
// PopUpGUIPanel, its key pressed once more, then enabled. Then the compass, the character sheet disabled for one
// object, the spell book disabled and enabled at once, ActionExamine with the examine panel disabled, the chat line,
// and last the game menu (it pauses the game).
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Note(string s) { Log("note|" + s); }
void Key(int k) { Log("key|" + IntToString(k) + "|tap"); }
void Disable(int nPanel, int bOn, object oTarget = OBJECT_INVALID)
{
    Note("disable|" + IntToString(nPanel) + "|" + IntToString(bOn));
    SetGuiPanelDisabled(GetFirstPC(), nPanel, bOn, oTarget);
}
void Pop(int nPanel) { Note("popup|" + IntToString(nPanel)); PopUpGUIPanel(GetFirstPC(), nPanel); }
void Flip() { Note("flip"); SetGuiPanelDisabled(GetFirstPC(), GUI_PANEL_SPELLBOOK, TRUE); SetGuiPanelDisabled(GetFirstPC(), GUI_PANEL_SPELLBOOK, FALSE); }
void Enable() { Note("enable"); SetGuiPanelDisabled(GetFirstPC(), GUI_PANEL_SPELLBOOK, FALSE); }
void FlipD() { Note("flipd"); SetGuiPanelDisabled(GetFirstPC(), GUI_PANEL_SPELLBOOK, TRUE); DelayCommand(0.0, Enable()); }
void Examine(object o, int bDisable)
{
    Note("examine|disabled=" + IntToString(bDisable));
    SetGuiPanelDisabled(GetFirstPC(), GUI_PANEL_EXAMINE_CREATURE, bDisable);
    AssignCommand(GetFirstPC(), ActionExamine(o));
}
void Panel(float t, int nPanel, int nKey, string sName)
{
    DelayCommand(t, Note("step|" + sName));
    DelayCommand(t, Key(nKey));
    DelayCommand(t + 1.5, Disable(nPanel, TRUE));
    DelayCommand(t + 2.5, VShot(sName + "_1_disabled"));
    DelayCommand(t + 3.0, Note("step|" + sName + "|key"));
    DelayCommand(t + 3.0, Key(nKey));
    DelayCommand(t + 4.0, VShot(sName + "_2_key"));
    DelayCommand(t + 4.5, Pop(nPanel));
    DelayCommand(t + 5.5, VShot(sName + "_3_popup"));
    DelayCommand(t + 6.0, Note("step|" + sName + "|key2"));
    DelayCommand(t + 6.0, Key(nKey));
    DelayCommand(t + 7.0, VShot(sName + "_4_key"));
    DelayCommand(t + 7.5, Disable(nPanel, FALSE));
}
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetEventScript(GetModule(), EVENT_SCRIPT_MODULE_ON_PLAYER_GUIEVENT, "vis_gui");
    object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(22.0, 15.0, 0.0), 180.0), FALSE, "GP_NPC");
    ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT); SetPlotFlag(n, TRUE);
    DelayCommand(1.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(2.5, VShot("a_hud"));
    Panel(3.0, GUI_PANEL_INVENTORY, 73, "b_inv");
    Panel(12.0, GUI_PANEL_CHARACTERSHEET, 67, "c_char");
    Panel(21.0, GUI_PANEL_JOURNAL, 74, "d_journal");
    Panel(30.0, GUI_PANEL_MINIMAP, 77, "e_map");
    Panel(39.0, GUI_PANEL_SPELLBOOK, 66, "f_book");
    Panel(48.0, GUI_PANEL_PLAYERLIST, 80, "g_players");
    // The compass: disabled, clicked, enabled, clicked.
    DelayCommand(57.0, Disable(GUI_PANEL_COMPASS, TRUE));
    DelayCommand(58.0, VShot("h_compass_disabled"));
    DelayCommand(58.5, Note("step|compass_click_disabled"));
    DelayCommand(58.5, Log("click|0.969,0.871"));
    DelayCommand(59.5, Disable(GUI_PANEL_COMPASS, FALSE));
    DelayCommand(60.0, Note("step|compass_click"));
    DelayCommand(60.0, Log("click|0.969,0.871"));
    // The character sheet disabled for another creature, then for the player (key, then the portrait).
    DelayCommand(61.0, Disable(GUI_PANEL_CHARACTERSHEET, TRUE, n));
    DelayCommand(61.5, Note("step|char_target_npc|key"));
    DelayCommand(61.5, Key(67));
    DelayCommand(62.5, VShot("i_char_target_npc"));
    DelayCommand(63.0, Key(67));
    DelayCommand(63.5, Disable(GUI_PANEL_CHARACTERSHEET, FALSE, n));
    DelayCommand(64.0, Disable(GUI_PANEL_CHARACTERSHEET, TRUE, pc));
    DelayCommand(64.5, Note("step|char_target_pc|key"));
    DelayCommand(64.5, Key(67));
    DelayCommand(65.5, VShot("i_char_target_pc"));
    DelayCommand(66.0, Note("step|char_target_pc|portrait"));
    DelayCommand(66.0, Log("click|0.969,0.07"));
    DelayCommand(67.0, VShot("i_char_portrait"));
    DelayCommand(67.5, Disable(GUI_PANEL_CHARACTERSHEET, FALSE, pc));
    // The spell book closed by disabling and enabling in one script, and with the enable a DelayCommand(0.0) later.
    DelayCommand(69.0, Key(66));
    DelayCommand(70.5, Flip());
    DelayCommand(71.5, VShot("j_book_flip"));
    DelayCommand(72.0, Note("step|key_after_flip"));
    DelayCommand(72.0, Key(66));
    DelayCommand(73.0, VShot("j_book_reopened"));
    DelayCommand(73.5, FlipD());
    DelayCommand(74.5, VShot("j_book_flipd"));
    // ActionExamine, then the same with the creature examine panel disabled.
    DelayCommand(75.5, Examine(n, FALSE));
    DelayCommand(77.0, VShot("k_examine"));
    DelayCommand(78.0, Examine(n, TRUE));
    DelayCommand(79.5, VShot("k_examine_disabled"));
    DelayCommand(80.0, SetGuiPanelDisabled(pc, GUI_PANEL_EXAMINE_CREATURE, FALSE));
    // The chat line's focus events.
    DelayCommand(85.0, Note("step|chat"));
    DelayCommand(85.0, Key(13));
    DelayCommand(86.0, VShot("l_chat_focus"));
    DelayCommand(87.0, Key(27));
    // The game menu pauses the game: shot and closed on the harness's clock.
    DelayCommand(88.0, Note("step|escmenu"));
    DelayCommand(88.0, Log("keyshot|27|m_escmenu|1.5|27"));
    DelayCommand(92.0, VDone());
}
