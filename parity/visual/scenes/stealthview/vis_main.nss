#include "visual_inc"
// How stealth looks to the player. Four neutral NPCs 4 m north of her: A in stealth that she can neither see nor
// hear, B in stealth that she only hears, C invisible (not in stealth), D in stealth that she has spotted. Then she
// enters stealth herself (owner view, floating text, chat), turns detect on and off with T (the variant without
// Keen Sense), and leaves stealth. Readings of what she perceives go to notes.txt.
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Perm(object o, effect e) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o); }

object Npc(object pc, string sTag, float dx)
{
    object o = VSpawn(pc, "nw_bandit001", dx, 4.0, 270.0);
    SetTag(o, sTag);
    // Not plot: OpenWinter's plot creatures refuse skill decreases (the original's readings were taken with plot set).
    SetPlotFlag(o, FALSE);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    return o;
}

void Senses(object pc, string sWhen)
{
    string s = sWhen;
    string t; int i;
    for (i = 0; i < 4; i++)
    {
        t = i == 0 ? "A" : i == 1 ? "B" : i == 2 ? "C" : "D";
        object o = GetObjectByTag("SV_" + t);
        s += "|" + t + " seen=" + IntToString(GetObjectSeen(o, pc)) + " heard=" + IntToString(GetObjectHeard(o, pc));
    }
    s += "|pc stealth=" + IntToString(GetActionMode(pc, ACTION_MODE_STEALTH)) + " detect=" + IntToString(GetActionMode(pc, ACTION_MODE_DETECT))
        + " keen=" + IntToString(GetHasFeat(FEAT_KEEN_SENSE, pc));
    Note(s);
}

void Key(int vk, string s) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(vk) + "|" + s); }

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    object a = Npc(pc, "SV_A", -4.5);
    object b = Npc(pc, "SV_B", -1.5);
    object c = Npc(pc, "SV_C", 1.5);
    object d = Npc(pc, "SV_D", 4.5);
    Perm(a, EffectSkillIncrease(SKILL_HIDE, 50)); Perm(a, EffectSkillIncrease(SKILL_MOVE_SILENTLY, 50));
    Perm(b, EffectSkillIncrease(SKILL_HIDE, 50)); Perm(b, EffectSkillDecrease(SKILL_MOVE_SILENTLY, 40));
    Perm(c, EffectInvisibility(INVISIBILITY_TYPE_NORMAL));
    Perm(d, EffectSkillDecrease(SKILL_HIDE, 40)); Perm(d, EffectSkillIncrease(SKILL_MOVE_SILENTLY, 50));
    SetActionMode(a, ACTION_MODE_STEALTH, TRUE);
    SetActionMode(b, ACTION_MODE_STEALTH, TRUE);
    SetActionMode(d, ACTION_MODE_STEALTH, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 55.0));
    DelayCommand(5.0, Senses(pc, "t5"));
    DelayCommand(12.0, Senses(pc, "t12"));
    DelayCommand(12.5, VShot("a_npcs"));
    DelayCommand(14.0, Key(9, "down"));
    DelayCommand(15.0, VShot("b_npcs_tab"));
    DelayCommand(16.5, Key(9, "up"));
    DelayCommand(18.0, SetActionMode(pc, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(18.4, VShot("c_pc_stealth_text"));
    DelayCommand(21.0, Senses(pc, "t21"));
    DelayCommand(21.5, VShot("d_pc_stealth"));
    DelayCommand(23.0, VCamera(pc, 90.0, 4.0, 70.0));
    DelayCommand(25.0, VShot("e_pc_stealth_close"));
    DelayCommand(26.0, VCamera(pc, 90.0, 10.0, 55.0));
    DelayCommand(28.0, Key(84, "tap"));
    DelayCommand(28.4, VShot("f_detect_text"));
    DelayCommand(31.0, Senses(pc, "t31"));
    DelayCommand(31.5, VShot("g_both_modes"));
    DelayCommand(33.0, Key(84, "tap"));
    DelayCommand(35.0, Senses(pc, "t35"));
    DelayCommand(36.0, SetActionMode(pc, ACTION_MODE_STEALTH, FALSE));
    DelayCommand(36.4, VShot("h_off_text"));
    DelayCommand(39.0, VShot("i_off"));
    DelayCommand(40.0, VDone());
}
