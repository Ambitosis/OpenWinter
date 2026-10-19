#include "visual_inc"
// mp_delivery: who gets a line, measured with one client (docs/MULTIPLAYER_HARNESS.md). Two original clients can't
// share a server on one CD key, so what another player is told is measured here through creatures around the
// player at known distances: speech by volume and distance, floating text with and without bBroadcastToFaction
// (a commoner, henchmen at 5, 25 and 35 m, the player), fights between two creatures that aren't hers at 4 to 45 m,
// and a creature casting Barkskin and Magic Missile near and far. Each case is announced in the chat window ("MPD <case>") so chat.txt shows what each one printed.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|mpd|" + s); }
void Mark(string sCase) { SendMessageToPC(GetFirstPC(), "MPD " + sCase); }

// A creature at fDistance metres from the player's start, fAngle degrees round from east.
object At(string sResRef, float fDistance, float fAngle, string sTag, int nFaction)
{
    object pc = GetFirstPC();
    vector p = Vector(8.0, 8.0, 0.0);
    location l = Location(GetArea(pc), Vector(p.x + fDistance * cos(fAngle), p.y + fDistance * sin(fAngle), 0.0), fAngle + 180.0);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, l, FALSE, sTag);
    if (nFaction >= 0) ChangeToStandardFaction(o, nFaction);
    SetPlotFlag(o, TRUE);
    return o;
}

string D(float f) { return IntToString(FloatToInt(f)); }

void Say(string sCase, float fDistance, int nVolume)
{
    object o = GetObjectByTag("mpd_" + D(fDistance));
    Mark(sCase + "_" + D(fDistance));
    Log(sCase + "_" + D(fDistance) + "|dist=" + FloatToString(GetDistanceBetween(o, GetFirstPC()), 0, 2));
    AssignCommand(o, SpeakString(sCase + " from " + D(fDistance) + " m", nVolume));
}

void Float(string sCase, object o, int bFaction)
{
    Mark(sCase);
    Log(sCase + "|dist=" + FloatToString(GetDistanceBetween(o, GetFirstPC()), 0, 2) + "|faction=" + IntToString(bFaction));
    FloatingTextStringOnCreature(sCase + " text", o, bFaction);
}

void Fight(string sCase, float fDistance)
{
    Mark(sCase);
    object a = At("nw_bandit001", fDistance, 30.0, "mpd_fa_" + sCase, STANDARD_FACTION_COMMONER);
    object b = At("nw_goblina", fDistance + 1.5, 30.0, "mpd_fb_" + sCase, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(a, FALSE);
    SetPlotFlag(b, FALSE);
    SetImmortal(a, TRUE);
    SetImmortal(b, TRUE);
    SetAILevel(a, AI_LEVEL_NORMAL);
    SetAILevel(b, AI_LEVEL_NORMAL);
    DelayCommand(0.5, AssignCommand(a, ActionAttack(b)));
    DelayCommand(0.5, AssignCommand(b, ActionAttack(a)));
    DelayCommand(8.5, Log(sCase + "|bandit_hp=" + IntToString(GetCurrentHitPoints(a)) + "|goblin_hp=" + IntToString(GetCurrentHitPoints(b))
        + "|bandit_action=" + IntToString(GetCurrentAction(a)) + "|goblin_action=" + IntToString(GetCurrentAction(b))));
    DelayCommand(9.0, DestroyObject(a));
    DelayCommand(9.0, DestroyObject(b));
}

// A creature that isn't hers casts a spell (cheat cast) at fDistance: Barkskin on itself, then Magic Missile at a
// goblin beside it.
void Cast(string sCase, float fDistance)
{
    Mark(sCase);
    object a = At("nw_bandit001", fDistance, 20.0, "mpd_ca_" + sCase, STANDARD_FACTION_COMMONER);
    object b = At("nw_goblina", fDistance + 3.0, 20.0, "mpd_cb_" + sCase, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(b, FALSE);
    SetImmortal(b, TRUE);
    SetAILevel(a, AI_LEVEL_NORMAL);
    DelayCommand(0.5, AssignCommand(a, ActionCastSpellAtObject(SPELL_BARKSKIN, a, METAMAGIC_ANY, TRUE)));
    DelayCommand(0.5, AssignCommand(a, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, b, METAMAGIC_ANY, TRUE)));
    DelayCommand(9.0, DestroyObject(a));
    DelayCommand(9.0, DestroyObject(b));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    SetMaxHenchmen(4);
    DelayCommand(1.0, VCamera(pc, 225.0, 25.0, 45.0));
    // Speakers at each distance, spread round the quarter in front of the player.
    At("nw_bandit001", 1.5, 5.0, "mpd_1", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 2.5, 15.0, "mpd_2", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 3.5, 25.0, "mpd_3", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 5.0, 35.0, "mpd_5", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 8.0, 45.0, "mpd_8", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 10.0, 55.0, "mpd_10", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 15.0, 65.0, "mpd_15", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 20.0, 75.0, "mpd_20", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 25.0, 85.0, "mpd_25", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 30.0, 10.0, "mpd_30", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 35.0, 20.0, "mpd_35", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 40.0, 40.0, "mpd_40", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 50.0, 50.0, "mpd_50", STANDARD_FACTION_COMMONER);
    At("nw_bandit001", 65.0, 45.0, "mpd_65", STANDARD_FACTION_COMMONER);
    object h5 = At("nw_bandit001", 5.0, 80.0, "mpd_h5", STANDARD_FACTION_COMMONER);
    object h25 = At("nw_bandit001", 25.0, 60.0, "mpd_h25", STANDARD_FACTION_COMMONER);
    object h35 = At("nw_bandit001", 35.0, 70.0, "mpd_h35", STANDARD_FACTION_COMMONER);

    float t = 6.0;
    // Talk at every distance, whisper close in, shout from far away.
    DelayCommand(t, Say("talk", 1.5, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("talk", 5.0, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("talk", 10.0, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("talk", 15.0, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("talk", 20.0, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("talk", 25.0, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("talk", 30.0, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("talk", 35.0, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("talk", 40.0, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("talk", 50.0, TALKVOLUME_TALK)); t += 1.2;
    DelayCommand(t, Say("whisper", 1.5, TALKVOLUME_WHISPER)); t += 1.2;
    DelayCommand(t, Say("whisper", 2.5, TALKVOLUME_WHISPER)); t += 1.2;
    DelayCommand(t, Say("whisper", 3.5, TALKVOLUME_WHISPER)); t += 1.2;
    DelayCommand(t, Say("whisper", 5.0, TALKVOLUME_WHISPER)); t += 1.2;
    DelayCommand(t, Say("whisper", 8.0, TALKVOLUME_WHISPER)); t += 1.2;
    DelayCommand(t, Say("whisper", 10.0, TALKVOLUME_WHISPER)); t += 1.2;
    DelayCommand(t, Say("shout", 50.0, TALKVOLUME_SHOUT)); t += 1.2;
    DelayCommand(t, Say("shout", 65.0, TALKVOLUME_SHOUT)); t += 1.2;
    DelayCommand(t, VShot("a_speech")); t += 2.0;

    // Floating text: a commoner, the player, then henchmen (her faction) near and far.
    DelayCommand(t, Float("float_commoner_faction", GetObjectByTag("mpd_5"), TRUE)); t += 1.2;
    DelayCommand(t, Float("float_commoner_only", GetObjectByTag("mpd_5"), FALSE)); t += 1.2;
    DelayCommand(t, Float("float_self_faction", pc, TRUE)); t += 1.2;
    DelayCommand(t, Float("float_self_only", pc, FALSE)); t += 1.2;
    DelayCommand(t, AddHenchman(pc, h5));
    DelayCommand(t, AddHenchman(pc, h25));
    DelayCommand(t, AddHenchman(pc, h35)); t += 1.0;
    DelayCommand(t, Float("float_hench5_faction", h5, TRUE)); t += 1.2;
    DelayCommand(t, Float("float_hench5_only", h5, FALSE)); t += 1.2;
    DelayCommand(t, Float("float_hench25_faction", h25, TRUE)); t += 1.2;
    DelayCommand(t, Float("float_hench35_faction", h35, TRUE)); t += 1.2;
    DelayCommand(t, VShot("b_float")); t += 2.0;

    // A fight that isn't hers, beside her and far away.
    DelayCommand(t, Fight("fight_4", 4.0)); t += 10.0;
    DelayCommand(t, Fight("fight_12", 12.0)); t += 10.0;
    DelayCommand(t, Fight("fight_20", 20.0)); t += 10.0;
    DelayCommand(t, Fight("fight_30", 30.0)); t += 10.0;
    DelayCommand(t, Fight("fight_45", 45.0)); t += 10.0;
    DelayCommand(t, Cast("cast_5", 5.0)); t += 10.0;
    DelayCommand(t, Cast("cast_45", 45.0)); t += 10.0;
    DelayCommand(t, VShot("c_fight")); t += 2.0;
    DelayCommand(t, Mark("end"));
    DelayCommand(t + 1.0, VDone());
}
