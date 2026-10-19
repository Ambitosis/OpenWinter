// Object highlighting: the mouse-over highlight on a neutral creature, a hostile one and a placeable,
// and every object lit by holding Tab. Not in cutscene mode (which ignores the mouse), so the GUI shows.
// The harness posts the mouse position (VISUAL|hover) and the held key (VISUAL|key) to the original's
// window without moving the real cursor or taking focus.
#include "visual_inc"

void Hover(string sAt)
{
    WriteTimestampedLogEntry("VISUAL|hover|" + sAt);
}

void Key(int nVk, string sState)
{
    WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|" + sState);
}

void Freeze(object o)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), o);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(oPC, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(oPC, TRUE);
    VHidePC(oPC);
    object n = VSpawn(oPC, "nw_bandit001", 0.0, 2.0, 270.0);
    Freeze(n);
    vector v = GetPosition(oPC);
    object h = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(GetArea(oPC), Vector(v.x + 1.8, v.y + 2.0, v.z), 270.0));
    ChangeToStandardFaction(h, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(h, TRUE);
    Freeze(h);
    AssignCommand(h, SetCommandable(FALSE, h));
    object p = VPlaceable(oPC, "plc_chest1", -1.8, 2.0, 0.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 7.0, 60.0));
    DelayCommand(6.0, VShot("a_none"));
    // Sweep the pointer over the middle creature (the camera looks at the hidden player, 2 m south of it).
    DelayCommand(7.0, Hover("0.50,0.40"));
    DelayCommand(9.0, VShot("b_hover_40"));
    DelayCommand(10.0, Hover("0.50,0.33"));
    DelayCommand(12.0, VShot("b_hover_33"));
    DelayCommand(13.0, Hover("0.50,0.47"));
    DelayCommand(15.0, VShot("b_hover_47"));
    // The hostile goblin (left) and the chest (right).
    DelayCommand(16.0, Hover("0.30,0.42"));
    DelayCommand(18.0, VShot("c_hover_left"));
    DelayCommand(19.0, Hover("0.70,0.45"));
    DelayCommand(21.0, VShot("d_hover_right"));
    DelayCommand(22.0, Hover("0.95,0.95"));
    // Everything, Tab held (VK_TAB = 9).
    DelayCommand(23.0, Key(9, "down"));
    DelayCommand(25.0, VShot("e_tab"));
    DelayCommand(26.0, Key(9, "up"));
    // A closer view of the hovered creature, to see the edge.
    DelayCommand(27.0, VCamera(oPC, 90.0, 3.5, 75.0));
    DelayCommand(28.0, Hover("0.50,0.40"));
    DelayCommand(30.0, VShot("f_close_hover"));
    DelayCommand(31.0, Hover("off"));
    DelayCommand(32.0, VShot("f_close_none"));
    // A closer look at all three under Tab.
    DelayCommand(33.0, VCamera(oPC, 90.0, 4.5, 68.0));
    DelayCommand(34.0, Key(9, "down"));
    DelayCommand(36.0, VShot("g_tab_close"));
    DelayCommand(37.0, Key(9, "up"));
    DelayCommand(39.0, VDone());
}
