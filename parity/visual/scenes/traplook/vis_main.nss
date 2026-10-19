// How traps look and what a player can do with them: a detected hostile trap trigger, a flagged one, one the
// player set with a kit, an inactive detected one and a detected trapped chest, from above and low down; a left
// click and the radial menu on a trap trigger (real pointer, monitor 2); the area map. Then more rules that need a
// player: passive Search range in and out of detect mode, a player walking into a commoner trap, a commoner walking
// into the player's trap, and a failed disarm in combat (no taking 20).
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
object ThePC() { return GetFirstPC(); }
location At(float dx, float dy, float f = 0.0)
{
    vector v = GetPositionFromLocation(GetLocalLocation(GetModule(), "ORIGIN"));
    return Location(GetArea(ThePC()), Vector(v.x + dx, v.y + dy, v.z), f);
}
string Secs() { return FloatToString(IntToFloat(GetTimeMinute() * 60 + GetTimeSecond()) + GetTimeMillisecond() / 1000.0, 0, 2); }

object Trap(string sTag, float dx, float dy, float fSize, int nFaction = STANDARD_FACTION_HOSTILE, int nDC = 250)
{
    object t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, At(dx, dy), fSize, sTag, nFaction, "tl_disarm", "tl_trig");
    SetTrapDetectDC(t, nDC);
    SetEventScript(t, EVENT_SCRIPT_TRIGGER_ON_OBJECT_ENTER, "tl_enter");
    return t;
}

object Npc(string sTag, float dx, float dy, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", At(dx, dy), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Setup()
{
    object pc = ThePC();
    SetLocalLocation(GetModule(), "ORIGIN", GetLocation(pc));
    SetImmortal(pc, TRUE);
    Note("character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    object h = Trap("P_HOSTILE", -5.0, 3.0, 1.5);
    SetTrapDetectedBy(h, pc);
    object i = Trap("P_INACTIVE", 5.0, 0.0, 1.5);
    SetTrapActive(i, FALSE);
    SetTrapDetectedBy(i, pc);
    object f = Trap("P_FLAG", -3.0, -5.0, 1.0);
    SetTrapDetectedBy(f, pc);
    SetTrapDisarmDC(f, 1);
    object u = Trap("P_UNSEEN", 3.0, -5.0, 1.0);
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", At(-2.0, 6.0), FALSE, "P_CHEST");
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, STANDARD_FACTION_HOSTILE, "tl_disarm", "tl_trig");
    SetTrapDetectDC(c, 250);
    SetTrapDetectedBy(c, pc);
    object c2 = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", At(2.0, 6.0), FALSE, "P_CHEST_PLAIN");
    // The player flags one trap, then sets one with a kit, then comes back.
    object kit = CreateItemOnObject("nw_it_trap001", pc);
    AssignCommand(pc, ActionUseSkill(SKILL_DISABLE_TRAP, f, SUBSKILL_FLAGTRAP));
    AssignCommand(pc, ActionUseItemAtLocation(kit, GetFirstItemProperty(kit), At(0.0, 5.0)));
    AssignCommand(pc, ActionDoCommand(Note("setup|flagged=" + IntToString(GetTrapFlagged(f)) + "|t=" + Secs())));
    AssignCommand(pc, ActionMoveToLocation(At(0.0, 0.0)));
    AssignCommand(pc, ActionDoCommand(SetFacing(90.0)));
}

void Look()
{
    object pc = ThePC();
    object s = GetNearestTrapToObject(pc, FALSE);
    object t = GetFirstObjectInShape(SHAPE_SPHERE, 1.0, At(0.0, 5.0), FALSE, OBJECT_TYPE_TRIGGER);
    SetTag(t, "P_SET");
    Note("look|set=" + IntToString(GetIsObjectValid(t)) + "|setdetected=" + IntToString(GetTrapDetectedBy(t, pc)) + "|flag=" + IntToString(GetTrapFlagged(GetObjectByTag("P_FLAG")))
        + "|pcpos=" + FloatToString(GetPosition(pc).x, 0, 2) + "," + FloatToString(GetPosition(pc).y, 0, 2));
    VCamera(pc, 90.0, 14.0, 5.0);
}

void Radial()
{
    VCamera(ThePC(), 90.0, 14.0, 5.0);
}

void AfterClick()
{
    object pc = ThePC();
    Note("click|action=" + IntToString(GetCurrentAction(pc)) + "|t=" + Secs());
}

void Reset()
{
    object pc = ThePC();
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(At(0.0, 0.0, 90.0)));
    Note("reset|inactive.trapped=" + IntToString(GetIsTrapped(GetObjectByTag("P_INACTIVE"))) + "|t=" + Secs());
}

void Rules1()
{
    object pc = ThePC();
    // Clear the presentation traps.
    DestroyObject(GetObjectByTag("P_HOSTILE")); DestroyObject(GetObjectByTag("P_INACTIVE")); DestroyObject(GetObjectByTag("P_FLAG"));
    DestroyObject(GetObjectByTag("P_UNSEEN")); DestroyObject(GetObjectByTag("P_CHEST"));
    Note("rules|detectmode=" + IntToString(GetActionMode(pc, ACTION_MODE_DETECT)) + "|search=" + IntToString(GetSkillRank(SKILL_SEARCH, pc)));
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSkillIncrease(SKILL_SEARCH, 50), pc, 30.0);
    // Detect mode as the player starts (on): DC 1 traps 6.5, 8, 10 and 12 m away.
    Trap("R6", 6.5, 0.0, 1.0, STANDARD_FACTION_HOSTILE, 1);
    Trap("R8", 0.0, 8.0, 1.0, STANDARD_FACTION_HOSTILE, 1);
    Trap("R10", -10.0, 0.0, 1.0, STANDARD_FACTION_HOSTILE, 1);
    Trap("R12", 0.0, -12.0, 1.0, STANDARD_FACTION_HOSTILE, 1);
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", At(7.0, 7.0), FALSE, "RC10");
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, STANDARD_FACTION_HOSTILE);
    SetTrapDetectDC(c, 1);
    Note("rules1|start|t=" + Secs());
}

void Poll(string sList, int nLeft)
{
    object pc = ThePC();
    string s = sList;
    while (GetStringLength(s) > 0)
    {
        int n = FindSubString(s, " ");
        string k = GetStringLeft(s, n);
        s = GetStringRight(s, GetStringLength(s) - n - 1);
        object t = GetObjectByTag(k);
        if (GetIsObjectValid(t) && !GetLocalInt(t, "SEEN") && GetTrapDetectedBy(t, pc))
        {
            SetLocalInt(t, "SEEN", 1);
            Note("detect|" + k + "|t=" + Secs() + "|dist=" + FloatToString(GetDistanceBetween(pc, t), 0, 2) + "|last=" + GetTag(GetLastTrapDetected(pc)));
        }
    }
    if (nLeft > 0) DelayCommand(0.25, Poll(sList, nLeft - 1));
}

void Rules2()
{
    object pc = ThePC();
    SetActionMode(pc, ACTION_MODE_DETECT, FALSE);
    Note("rules2|detectmode=" + IntToString(GetActionMode(pc, ACTION_MODE_DETECT)) + "|t=" + Secs());
    // Detect mode off: DC 1 traps 3, 4.5, 6 and 7 m away (diagonals).
    Trap("Q3", 2.12, 2.12, 0.5, STANDARD_FACTION_HOSTILE, 1);
    Trap("Q45", -3.18, 3.18, 0.5, STANDARD_FACTION_HOSTILE, 1);
    Trap("Q6", -4.24, -4.24, 0.5, STANDARD_FACTION_HOSTILE, 1);
    Trap("Q7", 4.95, -4.95, 0.5, STANDARD_FACTION_HOSTILE, 1);
}

void Rules3()
{
    object pc = ThePC();
    SetActionMode(pc, ACTION_MODE_DETECT, TRUE);
    // The player walks across a commoner trap; a commoner walks across the player's trap.
    Trap("COMMONER_TRAP", 0.0, -3.0, 1.0, STANDARD_FACTION_COMMONER);
    AssignCommand(pc, ActionMoveToLocation(At(0.0, -6.0)));
    AssignCommand(pc, ActionMoveToLocation(At(0.0, 0.0)));
    object n = Npc("COMMONER", -4.0, 5.0, STANDARD_FACTION_COMMONER);
    AssignCommand(n, ActionMoveToLocation(At(4.0, 5.0)));
}

void Rules4()
{
    object pc = ThePC();
    Note("rules4|commonertrap.valid=" + IntToString(GetIsObjectValid(GetObjectByTag("COMMONER_TRAP"))) + "|set.valid=" + IntToString(GetIsObjectValid(GetObjectByTag("P_SET"))));
    // A failed disarm in combat: d20 + bonus can't reach the DC, and misses it by 5 or more on any roll.
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", At(2.0, 0.0), FALSE, "C_FIGHT");
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, STANDARD_FACTION_HOSTILE, "tl_disarm", "tl_trig");
    SetTrapDisarmDC(c, GetSkillRank(SKILL_DISABLE_TRAP, pc) + 2 + 26);
    SetTrapDetectedBy(c, pc);
    object h = Npc("ATTACKER", -1.5, 0.0, STANDARD_FACTION_HOSTILE);
    AssignCommand(h, ActionAttack(pc));
}

void Rules5()
{
    object pc = ThePC();
    Note("rules5|incombat=" + IntToString(GetIsInCombat(pc)) + "|t=" + Secs());
    AssignCommand(pc, ActionUseSkill(SKILL_DISABLE_TRAP, GetObjectByTag("C_FIGHT")));
    AssignCommand(pc, ActionDoCommand(Note("rules5|done|trapped=" + IntToString(GetIsTrapped(GetObjectByTag("C_FIGHT"))) + "|t=" + Secs())));
}

void End()
{
    Note("end|fight.trapped=" + IntToString(GetIsTrapped(GetObjectByTag("C_FIGHT"))));
    DestroyObject(GetObjectByTag("ATTACKER"));
    VDone();
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    DelayCommand(0.5, Setup());
    DelayCommand(16.0, Look());
    DelayCommand(17.0, VShot("a_top"));
    DelayCommand(19.0, VCamera(pc, 90.0, 9.0, 50.0));
    DelayCommand(20.0, VShot("b_angled"));
    DelayCommand(22.0, VCamera(pc, 90.0, 5.0, 75.0));
    DelayCommand(23.0, VShot("c_low"));
    // A real left click on the inactive trap 5 m east, then the radial menu there.
    DelayCommand(25.0, Radial());
    DelayCommand(26.0, VShot("d_before_click"));
    DelayCommand(27.0, WriteTimestampedLogEntry("VISUAL|mouse|0.72,0.5|down"));
    DelayCommand(27.15, WriteTimestampedLogEntry("VISUAL|mouse|0.72,0.5|up"));
    DelayCommand(27.8, AfterClick());
    DelayCommand(28.0, VShot("e_after_click"));
    DelayCommand(29.5, AfterClick());
    DelayCommand(30.0, Reset());
    DelayCommand(31.0, Radial());
    DelayCommand(31.5, VShot("f_hover"));
    DelayCommand(33.0, WriteTimestampedLogEntry("VISUAL|mouse|0.72,0.5|down|right"));
    DelayCommand(33.8, VShot("g_radial_root"));
    DelayCommand(34.6, WriteTimestampedLogEntry("VISUAL|mouse|0.72,0.5|up|right"));
    DelayCommand(35.2, VShot("h_radial_released"));
    DelayCommand(36.5, WriteTimestampedLogEntry("VISUAL|key|27|tap"));
    // The area map.
    DelayCommand(38.0, WriteTimestampedLogEntry("VISUAL|key|77|tap"));
    DelayCommand(39.2, VShot("i_map"));
    DelayCommand(40.5, WriteTimestampedLogEntry("VISUAL|key|77|tap"));
    // Rules.
    DelayCommand(42.0, Rules1());
    DelayCommand(42.0, Poll("R6 R8 R10 R12 RC10 ", 36));
    DelayCommand(52.0, Rules2());
    DelayCommand(52.0, Poll("Q3 Q45 Q6 Q7 ", 36));
    DelayCommand(62.0, Rules3());
    DelayCommand(74.0, Rules4());
    DelayCommand(77.0, Rules5());
    DelayCommand(86.0, End());
}
