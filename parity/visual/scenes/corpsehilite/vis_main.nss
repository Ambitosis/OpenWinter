// Which corpses light up? Pavel's room (Prelude m0q1d) with its m1q0ddeadgob goblins (SetIsDestroyable(FALSE), then
// their own death: dead, not destroyable, not selectable when dead), plus three goblins killed here: one selectable
// when dead (SetIsDestroyable(FALSE, FALSE, TRUE)), one lootable with an item to drop, and an ordinary one shot
// while it lies for its five seconds. Tab (the highlight key) is held for each; posted hovers try each body.
// Not in cutscene mode, so the GUI and the highlight key work.
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Hover(string sAt) { WriteTimestampedLogEntry("VISUAL|hover|" + sAt); }
void Key(int nVk, string sState) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|" + sState); }

void State(string when, object o)
{
    vector v = GetPosition(o);
    Note(when + " " + GetName(o) + " " + FloatToString(v.x, 0, 1) + "," + FloatToString(v.y, 0, 1) + " valid " + IntToString(GetIsObjectValid(o))
        + " dead " + IntToString(GetIsDead(o)) + " hp " + IntToString(GetCurrentHitPoints(o)) + " destroyable " + IntToString(GetIsDestroyable(o))
        + " selectable " + IntToString(GetIsSelectableWhenDead(o)) + " lootable " + IntToString(GetLootable(o)) + " raiseable " + IntToString(GetIsRaiseable(o)));
}

void Report(string when)
{
    int n = 0;
    object o = GetObjectByTag("M1Q0DDeadGoblin", n);
    while (GetIsObjectValid(o))
    {
        State(when, o);
        o = GetObjectByTag("M1Q0DDeadGoblin", ++n);
    }
    State(when, GetObjectByTag("VIS_SELGOB"));
    State(when, GetObjectByTag("VIS_LOOTGOB"));
    State(when, GetObjectByTag("VIS_DECAYGOB"));
}

object Goblin(float x, float y, string sTag, string sName)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(GetArea(GetFirstPC()), Vector(x, y, 0.0), 270.0), FALSE, sTag);
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetName(o, sName);
    return o;
}

void Kill(object o) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o); }

void Selectable()
{
    object o = Goblin(40.5, 104.5, "VIS_SELGOB", "SelGob");
    AssignCommand(o, SetIsDestroyable(FALSE, FALSE, TRUE));
    DelayCommand(0.5, Kill(o));
}

void Lootable()
{
    object o = Goblin(46.5, 103.5, "VIS_LOOTGOB", "LootGob");
    SetLootable(o, TRUE);
    CreateItemOnObject("nw_it_mpotion001", o);
    DelayCommand(0.5, Kill(o));
}

void Decaying()
{
    object o = Goblin(38.5, 108.0, "VIS_DECAYGOB", "DecayGob");
    DelayCommand(0.5, Kill(o));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    // Pavel, in the copied module's factions, would fight the player: keep him neutral and still.
    object pavel = GetObjectByTag("M1Q0DPavel");
    ChangeToStandardFaction(pavel, STANDARD_FACTION_MERCHANT);
    AssignCommand(pavel, ClearAllActions(TRUE));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), pavel);
    DelayCommand(1.0, Selectable());
    DelayCommand(1.0, Lootable());
    DelayCommand(2.0, VCamera(pc, 90.0, 11.0, 55.0));
    DelayCommand(4.0, Report("4s"));
    DelayCommand(5.0, VShot("a_none"));
    // Every object lit, Tab held (VK_TAB = 9).
    DelayCommand(6.0, Key(9, "down"));
    DelayCommand(8.0, VShot("b_tab"));
    DelayCommand(9.0, Key(9, "up"));
    DelayCommand(10.0, VShot("c_tab_released"));
    // Posted hovers over the bodies (the original may not take posted hovers; Pavel is the living control).
    DelayCommand(11.0, Hover("0.50,0.42"));
    DelayCommand(13.0, VShot("d_hover_a"));
    DelayCommand(14.0, Hover("0.66,0.45"));
    DelayCommand(16.0, VShot("d_hover_b"));
    DelayCommand(17.0, Hover("off"));
    // An ordinary goblin while it lies for its five seconds.
    DelayCommand(18.0, Decaying());
    DelayCommand(20.0, Key(9, "down"));
    DelayCommand(21.0, VShot("e_tab_decaying"));
    DelayCommand(22.0, Key(9, "up"));
    DelayCommand(22.5, Report("22s"));
    DelayCommand(23.0, VShot("f_decaying"));
    // Closer, flatter, Tab held again: the names under Tab.
    DelayCommand(26.0, VCamera(pc, 90.0, 6.5, 65.0));
    DelayCommand(28.0, Key(9, "down"));
    DelayCommand(30.0, VShot("g_tab_close"));
    DelayCommand(31.0, Key(9, "up"));
    DelayCommand(32.0, VShot("h_close_none"));
    DelayCommand(33.0, Report("33s"));
    // Posted hovers in the close view: the dead goblin by the barrel, the one behind it, LootGob.
    DelayCommand(34.0, Hover("0.77,0.335"));
    DelayCommand(36.0, VShot("i_hover_dead"));
    DelayCommand(37.0, Hover("0.773,0.205"));
    DelayCommand(39.0, VShot("j_hover_dead_far"));
    DelayCommand(40.0, Hover("0.676,0.45"));
    DelayCommand(42.0, VShot("k_hover_loot"));
    DelayCommand(43.0, Hover("off"));
    DelayCommand(45.0, VDone());
}
