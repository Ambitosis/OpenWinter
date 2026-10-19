#include "visual_inc"
// combatlines (docs/COMBAT_PRESENTATION.md): which fights between two creatures print attack and damage lines in the
// player's chat window. mp_delivery saw lines from a commoner bandit fighting a hostile goblin 4-30 m away, but there
// the player had three bandit henchmen (stock scripts, also named "Bandit") standing near the fights; the
// difficulty and convobusy scenes, with no henchmen, saw none. Here: the same fights with no associate near, a
// defender instead of a commoner, the player blinded, her henchman attacking the goblin, the goblin
// attacking her henchman, and finally mp_delivery's arrangement (idle henchmen with stock scripts near a fight).
// Each case is announced in the chat window ("CL <case>") so chat.txt shows what it printed.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|cl|" + s); }
void Mark(string sCase) { SendMessageToPC(GetFirstPC(), "CL " + sCase); }

object At(string sResRef, float fDistance, float fAngle, string sTag, int nFaction)
{
    object pc = GetFirstPC();
    location l = Location(GetArea(pc), Vector(8.0 + fDistance * cos(fAngle), 8.0 + fDistance * sin(fAngle), 0.0), fAngle + 180.0);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, l, FALSE, sTag);
    if (nFaction >= 0) ChangeToStandardFaction(o, nFaction);
    return o;
}

// Fighters keep to the fight they are given: no event scripts (the goblin's own would turn on the player).
void Ready(object o)
{
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    SetPlotFlag(o, FALSE);
    SetImmortal(o, TRUE);
    SetAILevel(o, AI_LEVEL_NORMAL);
}

void Report(string sCase, object a, object b)
{
    Log(sCase + "|a_hp=" + IntToString(GetCurrentHitPoints(a)) + "|b_hp=" + IntToString(GetCurrentHitPoints(b))
        + "|a_action=" + IntToString(GetCurrentAction(a)) + "|b_action=" + IntToString(GetCurrentAction(b))
        + "|a_seen=" + IntToString(GetObjectSeen(a, GetFirstPC())) + "|b_seen=" + IntToString(GetObjectSeen(b, GetFirstPC()))
        + "|dist=" + FloatToString(GetDistanceBetween(b, GetFirstPC()), 0, 1));
}

// Two creatures fight for 8 s at fDistance: a (faction nA) and a hostile goblin.
void Fight(string sCase, float fDistance, int nA, int bInvisible)
{
    Mark(sCase);
    object a = At("nw_bandit001", fDistance, 30.0, "cl_a_" + sCase, nA);
    object b = At("nw_goblina", fDistance + 1.5, 30.0, "cl_b_" + sCase, STANDARD_FACTION_HOSTILE);
    Ready(a); Ready(b);
    // Blinded for this fight: she perceives neither fighter.
    if (bInvisible) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectBlindness(), GetFirstPC(), 8.6);
    DelayCommand(0.5, AssignCommand(a, ActionAttack(b)));
    DelayCommand(0.5, AssignCommand(b, ActionAttack(a)));
    DelayCommand(8.0, Report(sCase, a, b));
    DelayCommand(8.5, DestroyObject(a));
    DelayCommand(8.5, DestroyObject(b));
}

// Her henchman (scripts cleared) fights a hostile goblin at fDistance; bHenchAttacks: the henchman attacks,
// else only the goblin does.
void HenchFight(string sCase, float fDistance, int bHenchAttacks)
{
    object pc = GetFirstPC();
    Mark(sCase);
    object h = At("nw_bandit001", fDistance, 60.0, "cl_h_" + sCase, STANDARD_FACTION_COMMONER);
    AddHenchman(pc, h);
    object b = At("nw_goblina", fDistance + 1.5, 60.0, "cl_b_" + sCase, STANDARD_FACTION_HOSTILE);
    Ready(h); Ready(b);
    if (bHenchAttacks) DelayCommand(0.5, AssignCommand(h, ActionAttack(b)));
    DelayCommand(0.5, AssignCommand(b, ActionAttack(h)));
    DelayCommand(8.0, Report(sCase, h, b));
    DelayCommand(8.4, RemoveHenchman(pc, h));
    DelayCommand(8.5, DestroyObject(h));
    DelayCommand(8.5, DestroyObject(b));
}

// mp_delivery's arrangement: idle henchmen with the bandit's own stock scripts at 5 and 25 m, then a commoner
// bandit and a hostile goblin fighting 12 m away.
void Henchmen()
{
    object pc = GetFirstPC();
    Mark("henchmen_join");
    object h5 = At("nw_bandit001", 5.0, 80.0, "cl_h5", STANDARD_FACTION_COMMONER);
    object h25 = At("nw_bandit001", 25.0, 60.0, "cl_h25", STANDARD_FACTION_COMMONER);
    SetPlotFlag(h5, TRUE); SetPlotFlag(h25, TRUE);
    AddHenchman(pc, h5);
    AddHenchman(pc, h25);
    SetLocalObject(pc, "h5", h5);
    SetLocalObject(pc, "h25", h25);
}
void HenchmenFight()
{
    object pc = GetFirstPC();
    Fight("henchmen_fight_12", 12.0, STANDARD_FACTION_COMMONER, FALSE);
    DelayCommand(8.0, Log("henchmen|h5_action=" + IntToString(GetCurrentAction(GetLocalObject(pc, "h5")))
        + "|h25_action=" + IntToString(GetCurrentAction(GetLocalObject(pc, "h25")))
        + "|h5_target=" + GetTag(GetAttackTarget(GetLocalObject(pc, "h5")))
        + "|h25_target=" + GetTag(GetAttackTarget(GetLocalObject(pc, "h25")))));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetMaxHenchmen(4);
    DelayCommand(1.0, VCamera(pc, 225.0, 25.0, 45.0));
    float t = 4.0;
    DelayCommand(t, Fight("fight_4", 4.0, STANDARD_FACTION_COMMONER, FALSE)); t += 10.0;
    DelayCommand(t, Fight("fight_12", 12.0, STANDARD_FACTION_COMMONER, FALSE)); t += 10.0;
    DelayCommand(t, Fight("defender_4", 4.0, STANDARD_FACTION_DEFENDER, FALSE)); t += 10.0;
    DelayCommand(t, Fight("blind_4", 4.0, STANDARD_FACTION_COMMONER, TRUE)); t += 10.0;
    DelayCommand(t, HenchFight("hench_attacks_12", 12.0, TRUE)); t += 10.0;
    DelayCommand(t, HenchFight("hench_attacked_12", 12.0, FALSE)); t += 10.0;
    DelayCommand(t, VShot("a_mid")); t += 1.0;
    DelayCommand(t, Henchmen()); t += 2.0;
    DelayCommand(t, HenchmenFight()); t += 10.0;
    DelayCommand(t, VShot("b_end")); t += 1.0;
    DelayCommand(t, Mark("end"));
    DelayCommand(t + 1.0, VDone());
}
