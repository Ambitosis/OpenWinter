#include "visual_inc"
// The player's own stealth over time: alone (bursts of frames), then with a sharp-eyed neutral watcher coming
// into range, which spots her; an invisible NPC she only hears; an attack from stealth on a dummy (chat lines,
// sneak attack); trying to enter stealth while attacking. Readings go to notes.txt.
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Perm(object o, effect e) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o); }
void Burst(string s, float sec) { WriteTimestampedLogEntry("VISUAL|burst|" + s + "|" + FloatToString(sec, 0, 1) + "|4"); }

object Npc(object pc, string sTag, float dx, float dy)
{
    object o = VSpawn(pc, "nw_bandit001", dx, dy, 270.0);
    SetTag(o, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    return o;
}

void Senses(object pc, string sWhen)
{
    object w = GetObjectByTag("SS_WATCH"), i = GetObjectByTag("SS_INVIS"), d = GetObjectByTag("SS_DUMMY");
    Note(sWhen + "|pc stealth=" + IntToString(GetActionMode(pc, ACTION_MODE_STEALTH))
        + "|watcher sees pc=" + IntToString(GetObjectSeen(pc, w)) + " hears=" + IntToString(GetObjectHeard(pc, w))
        + "|dummy sees pc=" + IntToString(GetObjectSeen(pc, d)) + " hears=" + IntToString(GetObjectHeard(pc, d))
        + "|pc sees invis=" + IntToString(GetObjectSeen(i, pc)) + " hears=" + IntToString(GetObjectHeard(i, pc)));
}

void Watcher(object pc)
{
    object w = Npc(pc, "SS_WATCH", 4.0, 3.0);
    Perm(w, EffectSkillIncrease(SKILL_SPOT, 50));
    Perm(w, EffectSkillIncrease(SKILL_LISTEN, 50));
    SetActionMode(w, ACTION_MODE_DETECT, TRUE);
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    Perm(pc, EffectSkillIncrease(SKILL_HIDE, 30));
    Perm(pc, EffectSkillIncrease(SKILL_MOVE_SILENTLY, 30));
    Perm(pc, EffectBonusFeat(FEAT_SNEAK_ATTACK));
    // An invisible NPC that makes a lot of noise, 4 m north-west, and a blind-ish dummy 2 m north-east.
    object i = Npc(pc, "SS_INVIS", -4.0, 3.0);
    Perm(i, EffectInvisibility(INVISIBILITY_TYPE_NORMAL));
    Perm(i, EffectSkillDecrease(SKILL_MOVE_SILENTLY, 40));
    object d = Npc(pc, "SS_DUMMY", 1.5, 2.0);
    Perm(d, EffectSkillDecrease(SKILL_SPOT, 40));
    Perm(d, EffectSkillDecrease(SKILL_LISTEN, 40));
    Perm(d, EffectTemporaryHitpoints(300));
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 9.0, 55.0));
    DelayCommand(2.0, SetActionMode(pc, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(9.0, Senses(pc, "t9"));
    DelayCommand(9.0, VShot("a_alone"));
    DelayCommand(10.0, Burst("b_alone", 4.0));
    DelayCommand(15.0, Watcher(pc));
    DelayCommand(16.0, VShot("c_watcher_new"));
    DelayCommand(28.0, Senses(pc, "t28"));
    DelayCommand(28.5, Burst("d_watched", 3.0));
    DelayCommand(32.0, VShot("e_watched"));
    // Attack the dummy from stealth.
    DelayCommand(34.0, Senses(pc, "t34"));
    DelayCommand(34.5, AssignCommand(pc, ActionAttack(d)));
    DelayCommand(36.0, VShot("f_attack"));
    DelayCommand(37.0, SetActionMode(pc, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(37.5, Note("t37.5|stealth after retry while attacking=" + IntToString(GetActionMode(pc, ACTION_MODE_STEALTH))));
    DelayCommand(39.0, VShot("g_retry"));
    DelayCommand(40.0, AssignCommand(pc, ClearAllActions(TRUE)));
    DelayCommand(42.0, VShot("h_chat"));
    DelayCommand(43.0, VDone());
}
