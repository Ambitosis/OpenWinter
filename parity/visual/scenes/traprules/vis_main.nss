// Trap rules that need a player (nwserver NPCs never detect or set off trap triggers): passive Search
// range and timing, what the party learns, a player and a henchman walking into traps (event order, a
// detected trap on the path), Disable Trap's chat lines, experience and duration, Set Trap with a kit
// and who then knows about the trap. Readings go to notes.txt, the chat window to chat.txt.
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }

object ThePC() { return GetFirstPC(); }
location At(float dx, float dy, float f = 0.0)
{
    vector v = GetPositionFromLocation(GetLocalLocation(GetModule(), "ORIGIN"));
    return Location(GetArea(ThePC()), Vector(v.x + dx, v.y + dy, v.z), f);
}

object Trap(string sTag, float dx, float dy, int nDC, string sTrig = "tr_trig")
{
    object t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, At(dx, dy), 2.0, sTag, STANDARD_FACTION_HOSTILE, "tr_disarm", sTrig);
    SetTrapDetectDC(t, nDC);
    SetEventScript(t, EVENT_SCRIPT_TRIGGER_ON_OBJECT_ENTER, "tr_enter");
    SetEventScript(t, EVENT_SCRIPT_TRIGGER_ON_OBJECT_EXIT, "tr_exit");
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

object Chest(string sTag, float dx, float dy, int nDC)
{
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", At(dx, dy), FALSE, sTag);
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, STANDARD_FACTION_HOSTILE, "tr_disarm", "tr_trig");
    SetTrapDisarmDC(c, nDC);
    SetTrapDetectDC(c, 60);
    return c;
}

// Polls who has detected each listed trap and notes the first time.
void Watch(string sList, int nLeft)
{
    object pc = ThePC(), h = GetObjectByTag("HENCH");
    string s = sList;
    while (GetStringLength(s) > 0)
    {
        int n = FindSubString(s, " ");
        string k = GetStringLeft(s, n);
        s = GetStringRight(s, GetStringLength(s) - n - 1);
        object t = GetObjectByTag(k);
        if (!GetLocalInt(t, "PC") && GetTrapDetectedBy(t, pc))
        {
            SetLocalInt(t, "PC", 1);
            Note("detect|" + k + "|pc|" + FloatToString(GetTimeSecond() + GetTimeMillisecond() / 1000.0, 0, 2) + "|dist=" + FloatToString(GetDistanceBetween(pc, t), 0, 2));
        }
        if (!GetLocalInt(t, "H") && GetTrapDetectedBy(t, h))
        {
            SetLocalInt(t, "H", 1);
            Note("detect|" + k + "|hench|" + FloatToString(GetTimeSecond() + GetTimeMillisecond() / 1000.0, 0, 2));
        }
    }
    if (nLeft > 0) DelayCommand(0.25, Watch(sList, nLeft - 1));
}

void Setup()
{
    object pc = ThePC();
    SetLocalLocation(GetModule(), "ORIGIN", GetLocation(pc));
    SetImmortal(pc, TRUE);
    Note("character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    Note("pc|" + GetName(pc) + "|ispc=" + IntToString(GetIsPC(pc)) + "|disable=" + IntToString(GetSkillRank(SKILL_DISABLE_TRAP, pc))
        + "|search=" + IntToString(GetSkillRank(SKILL_SEARCH, pc)) + "|set=" + IntToString(GetSkillRank(SKILL_SET_TRAP, pc))
        + "|xp=" + IntToString(GetXP(pc)) + "|detectmode=" + IntToString(GetActionMode(pc, ACTION_MODE_DETECT)));
    object h = Npc("HENCH", -2.0, -2.0, STANDARD_FACTION_COMMONER);
    AddHenchman(pc, h);
    Note("hench|master=" + IntToString(GetMaster(h) == pc) + "|faction=" + IntToString(GetFactionEqual(h, pc)));
    // Phase 1: passive detection. +50 Search, traps of DC 1 at 5, 9, 14 and 20 m; one near the henchman only.
    effect e = EffectSkillIncrease(SKILL_SEARCH, 50);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, e, pc, 12.0);
    Trap("D5", 5.0, 0.0, 1);
    Trap("D9", 0.0, 9.0, 1);
    Trap("D14", -14.0, 0.0, 1);
    Trap("D20", 0.0, -20.0, 1);
    Watch("D5 D9 D14 D20 ", 48);
    VCamera(pc, 90.0, 20.0, 10.0);
}

void Phase1End()
{
    object pc = ThePC(), h = GetObjectByTag("HENCH");
    Note("p1|last=" + GetTag(GetLastTrapDetected(pc)) + "|hench.D5=" + IntToString(GetTrapDetectedBy(GetObjectByTag("D5"), h))
        + "|D20=" + IntToString(GetTrapDetectedBy(GetObjectByTag("D20"), pc)));
    VShot("a_detected_overview");
    // The henchman searches a trap 18 m from the player (beyond the player's reach) with +50.
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSkillIncrease(SKILL_SEARCH, 50), h, 10.0);
    AssignCommand(h, JumpToLocation(At(-2.0, 16.0)));
    Trap("HD", 0.0, 18.0, 1);
    Watch("HD ", 36);
}

void Phase2()
{
    object pc = ThePC(), h = GetObjectByTag("HENCH");
    Note("p2|hd.pc=" + IntToString(GetTrapDetectedBy(GetObjectByTag("HD"), pc)) + "|hd.hench=" + IntToString(GetTrapDetectedBy(GetObjectByTag("HD"), h)));
    AssignCommand(h, JumpToLocation(At(-2.0, -2.0)));
    // The player walks into an undetected trap 4 m east, then back west across a detected one.
    Trap("WALK", 4.0, 3.0, 60);
    object k = Trap("KNOWN", -4.0, 3.0, 60);
    SetTrapDetectedBy(k, pc, TRUE);
    AssignCommand(pc, ActionMoveToLocation(At(8.0, 3.0)));
    AssignCommand(pc, ActionMoveToLocation(At(-8.0, 3.0)));
    AssignCommand(pc, ActionDoCommand(Note("p2|walked|hp=" + IntToString(GetCurrentHitPoints(pc)))));
    AssignCommand(pc, ActionMoveToLocation(At(0.0, 0.0)));
}

void Track(int nLeft)
{
    object pc = ThePC();
    vector v = GetPosition(pc) - GetPositionFromLocation(GetLocalLocation(GetModule(), "ORIGIN"));
    Note("track|" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2));
    if (nLeft > 0) DelayCommand(0.5, Track(nLeft - 1));
}

void Phase3()
{
    object pc = ThePC(), h = GetObjectByTag("HENCH");
    Note("p3|walk.valid=" + IntToString(GetIsObjectValid(GetObjectByTag("WALK"))) + "|known.valid=" + IntToString(GetIsObjectValid(GetObjectByTag("KNOWN"))));
    // The henchman walks into a trap; so does a hostile creature.
    Trap("HWALK", -2.0, -6.0, 60);
    AssignCommand(h, ActionMoveToLocation(At(-2.0, -10.0)));
    object n = Npc("HOSTILE", 4.0, -2.0, STANDARD_FACTION_COMMONER);
    Trap("NWALK", 4.0, -6.0, 60);
    AssignCommand(n, ActionMoveToLocation(At(4.0, -10.0)));
}

void Phase4()
{
    object pc = ThePC();
    Note("p4|xp=" + IntToString(GetXP(pc)) + "|hwalk.valid=" + IntToString(GetIsObjectValid(GetObjectByTag("HWALK"))) + "|nwalk.valid=" + IntToString(GetIsObjectValid(GetObjectByTag("NWALK"))));
    // Disable Trap on chests: disarm, examine, flag, recover, and a failure by 8 (out of combat).
    object c1 = Chest("C_DISARM", 2.0, 2.0, 10);
    object c2 = Chest("C_EXAMINE", 2.0, -2.0, 10);
    object c3 = Chest("C_FLAG", -2.0, 2.0, 10);
    object c4 = Chest("C_RECOVER", -2.0, -2.0, 1);
    object c5 = Chest("C_FAIL", 0.0, 3.0, GetSkillRank(SKILL_DISABLE_TRAP, pc) + 28);
    SetTrapDetectedBy(c1, pc); SetTrapDetectedBy(c2, pc); SetTrapDetectedBy(c3, pc); SetTrapDetectedBy(c4, pc); SetTrapDetectedBy(c5, pc);
    AssignCommand(pc, ActionUseSkill(SKILL_DISABLE_TRAP, c1));
    AssignCommand(pc, ActionDoCommand(Note("p4|disarmed|trapped=" + IntToString(GetIsTrapped(c1)) + "|xp=" + IntToString(GetXP(pc)))));
    AssignCommand(pc, ActionUseSkill(SKILL_DISABLE_TRAP, c2, SUBSKILL_EXAMINETRAP));
    AssignCommand(pc, ActionDoCommand(Note("p4|examined|xp=" + IntToString(GetXP(pc)))));
    AssignCommand(pc, ActionUseSkill(SKILL_DISABLE_TRAP, c3, SUBSKILL_FLAGTRAP));
    AssignCommand(pc, ActionDoCommand(Note("p4|flagged|" + IntToString(GetTrapFlagged(c3)) + "|hench=" + IntToString(GetTrapDetectedBy(c3, GetObjectByTag("HENCH"))) + "|xp=" + IntToString(GetXP(pc)))));
    AssignCommand(pc, ActionUseSkill(SKILL_DISABLE_TRAP, c4, SUBSKILL_RECOVERTRAP));
    AssignCommand(pc, ActionDoCommand(Note("p4|recovered|trapped=" + IntToString(GetIsTrapped(c4)) + "|kit=" + IntToString(GetIsObjectValid(GetItemPossessedBy(pc, "NW_IT_TRAP001"))) + "|xp=" + IntToString(GetXP(pc)))));
    AssignCommand(pc, ActionUseSkill(SKILL_DISABLE_TRAP, c5));
    AssignCommand(pc, ActionDoCommand(Note("p4|failed|trapped=" + IntToString(GetIsTrapped(c5)) + "|hp=" + IntToString(GetCurrentHitPoints(pc)) + "|xp=" + IntToString(GetXP(pc)))));
}

void Phase5()
{
    object pc = ThePC();
    // Set Trap with a kit 3 m north.
    object kit = CreateItemOnObject("nw_it_trap001", pc);
    AssignCommand(pc, ClearAllActions());
    AssignCommand(pc, ActionUseItemAtLocation(kit, GetFirstItemProperty(kit), At(0.0, -4.0)));
    AssignCommand(pc, ActionDoCommand(Note("p5|set|kit=" + IntToString(GetIsObjectValid(kit)))));
}

void Phase5b()
{
    object pc = ThePC(), h = GetObjectByTag("HENCH");
    object t = GetNearestTrapToObject(pc, FALSE);
    vector v = GetPosition(t) - GetPositionFromLocation(GetLocalLocation(GetModule(), "ORIGIN"));
    SetTag(t, "SET");
    Note("p5|trap=" + IntToString(GetObjectType(t)) + "|at=" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2)
        + "|pc=" + IntToString(GetTrapDetectedBy(t, pc)) + "|hench=" + IntToString(GetTrapDetectedBy(t, h))
        + "|creator=" + IntToString(GetTrapCreator(t) == pc) + "|faction=" + IntToString(GetFactionEqual(t, pc))
        + "|detectdc=" + IntToString(GetTrapDetectDC(t)) + "|disarmdc=" + IntToString(GetTrapDisarmDC(t)) + "|name=" + GetName(t) + "|xp=" + IntToString(GetXP(pc)));
    SetEventScript(t, EVENT_SCRIPT_TRIGGER_ON_OBJECT_ENTER, "tr_enter");
    SetEventScript(t, EVENT_SCRIPT_TRIGGER_ON_TRAPTRIGGERED, "tr_trig");
    VShot("b_set_trap");
    // A hostile creature and then the henchman walk across it.
    object n = GetObjectByTag("HOSTILE");
    ChangeToStandardFaction(n, STANDARD_FACTION_HOSTILE);
    AssignCommand(n, JumpToLocation(At(-3.0, -4.0)));
    AssignCommand(n, ActionMoveToLocation(At(3.0, -4.0)));
}

void Phase6()
{
    object h = GetObjectByTag("HENCH");
    AssignCommand(h, ClearAllActions());
    AssignCommand(h, JumpToLocation(At(-3.0, -4.5)));
    AssignCommand(h, ActionMoveToLocation(At(3.0, -4.5)));
}

void Phase7()
{
    object pc = ThePC();
    // The player opens a trapped chest it hasn't found.
    object c = Chest("C_OPEN", 3.0, 0.0, 10);
    AssignCommand(pc, ClearAllActions());
    AssignCommand(pc, ActionInteractObject(c));
}

void End()
{
    object pc = ThePC();
    Note("end|set.valid=" + IntToString(GetIsObjectValid(GetObjectByTag("SET"))) + "|open.trapped=" + IntToString(GetIsTrapped(GetObjectByTag("C_OPEN")))
        + "|hp=" + IntToString(GetCurrentHitPoints(pc)) + "|xp=" + IntToString(GetXP(pc)));
    VDone();
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    DelayCommand(0.5, Setup());
    DelayCommand(12.5, Phase1End());
    DelayCommand(22.0, Phase2());
    DelayCommand(22.0, Track(28));
    DelayCommand(37.0, Phase3());
    DelayCommand(45.0, Phase4());
    DelayCommand(78.0, Phase5());
    DelayCommand(86.0, Phase5b());
    DelayCommand(93.0, Phase6());
    DelayCommand(100.0, Phase7());
    DelayCommand(108.0, End());
}
