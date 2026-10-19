// Area music on its own (run with only the music volume up: tools/visual/audio_scene.py run musicbattle --music 1):
// the day track on entry, MusicBackgroundChangeDay while it plays, a hostile in view, the hostile attacking the
// player, its death and the wait for the background music to come back, scripted battle music, stopping and
// restarting the background music, a short track repeating after MusicBackgroundSetDelay, and nightfall.
#include "visual_inc"

void Phase(string s) { WriteTimestampedLogEntry("VISUAL|note|musicbattle|phase|" + s); }

void Tracks(string s)
{
    object a = GetArea(GetFirstPC());
    WriteTimestampedLogEntry("VISUAL|note|musicbattle|tracks|" + s + "|" + IntToString(MusicBackgroundGetDayTrack(a)) + ","
        + IntToString(MusicBackgroundGetNightTrack(a)) + "," + IntToString(MusicBackgroundGetBattleTrack(a))
        + "|night=" + IntToString(GetIsNight()) + "|combat=" + IntToString(GetIsInCombat(GetFirstPC())));
}

void ChangeDay(int n) { Phase("changeday"); MusicBackgroundChangeDay(GetArea(GetFirstPC()), n); Tracks("changeday"); }

void Foe()
{
    Phase("hostile_seen");
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, "mb_foe", Location(GetArea(pc), Vector(v.x + 8.0, v.y, v.z), 180.0), FALSE, "MB_FOE");
    ChangeToStandardFaction(o, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(o, TRUE);
    AssignCommand(o, ClearAllActions(TRUE));
}

void FoeAttacks()
{
    Phase("foe_attacks");
    object o = GetObjectByTag("MB_FOE");
    AssignCommand(o, ActionAttack(GetFirstPC()));
    DelayCommand(1.0, Tracks("attacking"));
}

void Kill()
{
    Phase("kill");
    object o = GetObjectByTag("MB_FOE");
    SetPlotFlag(o, FALSE);
    AssignCommand(o, ClearAllActions(TRUE));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
    DelayCommand(1.0, Tracks("killed"));
    DelayCommand(10.0, Tracks("killed10"));
}

void Battle(int bPlay)
{
    object a = GetArea(GetFirstPC());
    if (bPlay) { Phase("battle_script"); MusicBattlePlay(a); }
    else { Phase("battle_script_stop"); MusicBattleStop(a); }
}

void Background(int bPlay)
{
    object a = GetArea(GetFirstPC());
    if (bPlay) { Phase("bg_play"); MusicBackgroundPlay(a); }
    else { Phase("bg_stop"); MusicBackgroundStop(a); }
}

void Short()
{
    Phase("short");
    object a = GetArea(GetFirstPC());
    MusicBackgroundSetDelay(a, 5000);
    MusicBackgroundChangeDay(a, 93);
}

void Night() { Phase("night"); SetTime(21, 0, 0, 0); DelayCommand(1.0, Tracks("night")); }

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    Phase("enter");
    Tracks("enter");
    SetPlotFlag(pc, TRUE);
    DelayCommand(0.5, VCamera(pc, 90.0, 10.0, 60.0));
    DelayCommand(2.0, VShot("start"));
    DelayCommand(15.0, ChangeDay(4));
    DelayCommand(27.0, Foe());
    DelayCommand(30.0, VShot("foe"));
    DelayCommand(40.0, FoeAttacks());
    DelayCommand(55.0, Kill());
    DelayCommand(100.0, Battle(TRUE));
    DelayCommand(110.0, Battle(FALSE));
    DelayCommand(122.0, Background(FALSE));
    DelayCommand(128.0, Background(TRUE));
    DelayCommand(138.0, Short());
    DelayCommand(188.0, Night());
    DelayCommand(203.0, VDone());
}
