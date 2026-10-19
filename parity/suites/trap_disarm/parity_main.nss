// Disable Trap through ActionUseSkill: disarm, flag, examine and recover against DCs placed relative to the
// actor's skill rank (take 20 or a d20, the subskill DC changes, failing by 5 or more), how long it takes,
// who must have detected the trap, rogue-only DCs, recovered kits, and trap triggers and doors.
#include "ptrap_inc"
#include "parity_time"

object Actor(string sTag, string sRes, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, PTrapL(x, y), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Watch(string k, object a, object t, int nLeft, int nWasTrapped, int nWasAction)
{
    int nTrapped = GetIsObjectValid(t) ? GetIsTrapped(t) : -1;
    int nAction = GetCurrentAction(a);
    float s = PSince() - GetLocalFloat(a, "T0");
    if (nTrapped != nWasTrapped)
    {
        PLogF(k + ".trapped" + IntToString(nTrapped) + ".at", s, 0.35);
        if (GetIsObjectValid(t)) PLogF(k + ".trapped" + IntToString(nTrapped) + ".dist", GetDistanceBetween(a, t), 0.3);
    }
    if (nAction != nWasAction)
    {
        PLogI(k + ".action." + IntToString(GetLocalInt(a, "NACT")), nAction);
        PLogF(k + ".action." + IntToString(GetLocalInt(a, "NACT")) + ".at", s, 0.35);
    }
    if (nAction != nWasAction) SetLocalInt(a, "NACT", GetLocalInt(a, "NACT") + 1);
    if (nLeft > 0) DelayCommand(0.1, Watch(k, a, t, nLeft - 1, nTrapped, nAction));
}

// One attempt: the trap's disarm DC is the actor's rank plus nOffset (or exactly nDC when nDC >= 0).
void Case(string k, string sRes, object t, int nSub, int nOffset, int nDC = -1, int nDetected = TRUE, int nBonus = 0, int nWatch = FALSE, float fDist = 3.0)
{
    vector v = GetPosition(t);
    object a = Actor("A_" + k, sRes, v.x - fDist, v.y);
    if (nBonus > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_DISABLE_TRAP, nBonus), a);
    int nRank = GetSkillRank(SKILL_DISABLE_TRAP, a);
    SetTrapDisarmDC(t, nDC >= 0 ? nDC : nRank + nOffset);
    if (nDetected) SetTrapDetectedBy(t, a, TRUE);
    PLogI(k + ".rank", nRank);
    PLogI(k + ".dc", GetTrapDisarmDC(t));
    SetLocalObject(GetModule(), "T_" + k, t);
    SetLocalFloat(a, "T0", PSince());
    AssignCommand(a, ActionUseSkill(SKILL_DISABLE_TRAP, t, nSub));
    AssignCommand(a, ActionDoCommand(SetLocalInt(a, "done", 1)));
    if (nWatch) Watch(k, a, t, 110, 1, -2);
}

object Chest(float x, float y)
{
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(x, y));
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    SetTag(c, "C_" + FloatToString(x, 0, 0) + "_" + FloatToString(y, 0, 0));
    return c;
}

string Items(object a)
{
    string s = "";
    object i = GetFirstItemInInventory(a);
    while (GetIsObjectValid(i))
    {
        if (GetBaseItemType(i) == BASE_ITEM_TRAPKIT) s += GetResRef(i) + "x" + IntToString(GetItemStackSize(i)) + ";";
        i = GetNextItemInInventory(a);
    }
    return s;
}

void Result(string k)
{
    object a = GetObjectByTag("A_" + k), t = GetLocalObject(GetModule(), "T_" + k);
    PLogI(k + ".done", GetLocalInt(a, "done"));
    PLogI(k + ".valid", GetIsObjectValid(t));
    PLogI(k + ".trapped", GetIsTrapped(t));
    PLogI(k + ".flagged", GetTrapFlagged(t));
    PLogI(k + ".detected", GetTrapDetectedBy(t, a));
    PLogI(k + ".detected.other", GetTrapDetectedBy(t, GetObjectByTag("OTHER")));
    PLog(k + ".kits", Items(a));
    PLogI(k + ".hurt", GetCurrentHitPoints(a) < GetMaxHitPoints(a));
    PTrapObjEvents(k, GetTag(t));
}

// The rolled (in combat) attempts: how many succeeded (about 30%: d20 + rank + 2 against rank + 17) and how many
// set their trap off (none: these traps and creatures are all NPCs').
void FightReport()
{
    int i, nOk = 0, nSprung = 0, nCombat = 0;
    for (i = 0; i < 12; i++)
    {
        string k = "FIGHT" + IntToString(i);
        object a = GetObjectByTag("A_" + k), t = GetLocalObject(GetModule(), "T_" + k);
        nOk += !GetIsTrapped(t) && FindSubString(GetLocalString(GetModule(), "PTO_" + GetTag(t)), "disarm") >= 0;
        nSprung += FindSubString(GetLocalString(GetModule(), "PTO_" + GetTag(t)), "trig") >= 0;
        nCombat += GetLocalInt(a, "COMBAT");
    }
    PLogI("fight.incombat", nCombat);
    PLog("fight.disarmed", IntToString(nOk) + "|~4");
    PLogI("fight.sprung", nSprung);
}

void Batch1()
{
    Actor("OTHER", "td_rogue", 5.0, 5.0);
    Case("SUCC", "td_rogue", Chest(10.0, 10.0), 0, -10, -1, TRUE, 0, TRUE);
    Case("FAILBIG", "td_rogue", Chest(20.0, 10.0), 0, 30, -1, TRUE, 0, TRUE);
    Case("FAILSMALL", "td_rogue", Chest(30.0, 10.0), 0, 22);
    Case("FAIL4", "td_rogue", Chest(40.0, 10.0), 0, 24);
    Case("FAIL5", "td_rogue", Chest(50.0, 10.0), 0, 25);
    Case("EXACT", "td_rogue", Chest(60.0, 10.0), 0, 20);
    int i;
    for (i = 0; i < 6; i++) Case("STAT" + IntToString(i), "td_rogue", Chest(10.0 + 10.0 * i, 18.0), 0, 15);
    Case("FLAG", "td_rogue", Chest(10.0, 26.0), SUBSKILL_FLAGTRAP, -10, -1, TRUE, 0, TRUE);
    Case("FLAGA", "td_rogue", Chest(20.0, 26.0), SUBSKILL_FLAGTRAP, 25);
    Case("FLAGB", "td_rogue", Chest(30.0, 26.0), SUBSKILL_FLAGTRAP, 26);
    Case("FLAGBIG", "td_rogue", Chest(40.0, 26.0), SUBSKILL_FLAGTRAP, 40);
    Case("EXAM", "td_rogue", Chest(50.0, 26.0), SUBSKILL_EXAMINETRAP, -10, -1, TRUE, 0, TRUE);
    Case("EXAMA", "td_rogue", Chest(60.0, 26.0), SUBSKILL_EXAMINETRAP, 27);
    Case("EXAMB", "td_rogue", Chest(70.0, 26.0), SUBSKILL_EXAMINETRAP, 28);
    Case("EXAMBIG", "td_rogue", Chest(70.0, 18.0), SUBSKILL_EXAMINETRAP, 40);
    Case("REC", "td_rogue", Chest(10.0, 34.0), SUBSKILL_RECOVERTRAP, -20, -1, TRUE, 0, TRUE);
    Case("RECA", "td_rogue", Chest(20.0, 34.0), SUBSKILL_RECOVERTRAP, 10);
    Case("RECB", "td_rogue", Chest(30.0, 34.0), SUBSKILL_RECOVERTRAP, 11);
    Case("RECFAIL", "td_rogue", Chest(40.0, 34.0), SUBSKILL_RECOVERTRAP, 16);
    object c = Chest(50.0, 34.0); SetTrapRecoverable(c, FALSE);
    Case("NOREC", "td_rogue", c, SUBSKILL_RECOVERTRAP, -20);
    c = Chest(60.0, 34.0); SetTrapDisarmable(c, FALSE);
    Case("NODIS", "td_rogue", c, 0, -10);
    Case("UNDET", "td_rogue", Chest(70.0, 34.0), 0, -10, -1, FALSE);
    Case("ROGUE35", "td_fighter", Chest(10.0, 42.0), 0, 0, 35, TRUE, 40);
    Case("ROGUE34", "td_fighter", Chest(20.0, 42.0), 0, 0, 34, TRUE, 40);
    Case("ROGUEFLAG35", "td_fighter", Chest(30.0, 42.0), SUBSKILL_FLAGTRAP, 0, 35, TRUE, 40);
    Case("ROGUEEXAM35", "td_fighter", Chest(40.0, 42.0), SUBSKILL_EXAMINETRAP, 0, 35, TRUE, 40);
    Case("UNTRAINED", "td_untrained", Chest(50.0, 42.0), 0, 0, 1);
    Case("FIGHTER", "td_fighter", Chest(60.0, 42.0), 0, -10);
    object t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(70.0, 42.0), 2.0, "TRIG", STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    Case("TRIG", "td_rogue", t, 0, -10, -1, TRUE, 0, TRUE);
    t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(10.0, 50.0), 2.0, "TRIGREC", STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    Case("TRIGREC", "td_rogue", t, SUBSKILL_RECOVERTRAP, -20);
    t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(20.0, 50.0), 2.0, "TRIGFAIL", STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    Case("TRIGFAIL", "td_rogue", t, 0, 30);
    Case("DOOR", "td_rogue", GetObjectByTag("T_DOOR"), 0, -10, -1, TRUE, 0, TRUE);
    Case("DOORFAIL", "td_rogue", GetObjectByTag("T_DOORF"), 0, 30);
}

// In combat (attacked by a hostile creature, plot so it can't be hurt): no taking 20.
void Fight(string k, float x, float y)
{
    object t = Chest(x, y);
    object a = Actor("A_" + k, "td_rogue", x - 1.5, y);
    SetPlotFlag(a, TRUE);
    object h = Actor("H_" + k, "nw_bandit001", x - 2.5, y);
    ChangeToStandardFaction(h, STANDARD_FACTION_HOSTILE);
    AssignCommand(h, ActionAttack(a));
    SetLocalObject(GetModule(), "T_" + k, t);
    SetTrapDisarmDC(t, GetSkillRank(SKILL_DISABLE_TRAP, a) + 17);
    SetTrapDetectedBy(t, a, TRUE);
}

void FightGo(string k)
{
    object a = GetObjectByTag("A_" + k), t = GetLocalObject(GetModule(), "T_" + k);
    SetLocalInt(a, "COMBAT", GetIsInCombat(a));
    AssignCommand(a, ActionUseSkill(SKILL_DISABLE_TRAP, t, 0));
    AssignCommand(a, ActionDoCommand(SetLocalInt(a, "done", 1)));
}

void Batch2()
{
    int i;
    for (i = 5; i <= 8; i++) Case("FLAGK" + IntToString(i), "td_rogue", Chest(10.0 + 10.0 * (i - 5), 58.0), SUBSKILL_FLAGTRAP, 22 + i);
    Case("RECK22", "td_rogue", Chest(50.0, 58.0), SUBSKILL_RECOVERTRAP, 12);
    Case("RECK23", "td_rogue", Chest(60.0, 58.0), SUBSKILL_RECOVERTRAP, 13);
    Case("ROGUE36", "td_fighter", Chest(10.0, 66.0), 0, 0, 36, TRUE, 40);
    Case("ROGUE60", "td_fighter", Chest(20.0, 66.0), 0, 0, 60, TRUE, 40);
    Case("ROGUEOK60", "td_rogue", Chest(30.0, 66.0), 0, 0, 60, TRUE, 40);
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(40.0, 66.0), FALSE, "C_DEADLY");
    CreateTrapOnObject(TRAP_BASE_TYPE_DEADLY_FIRE, c, STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    Case("ROGUEDEADLY", "td_fighter", c, 0, 0, 20, TRUE, 40);
    Case("ROGUEFLAG36", "td_fighter", Chest(70.0, 58.0), SUBSKILL_FLAGTRAP, 0, 36, TRUE, 40);
    Case("ROGUEREC36", "td_fighter", Chest(60.0, 50.0), SUBSKILL_RECOVERTRAP, 0, 36, TRUE, 40);
    Case("NEAR", "td_rogue", Chest(50.0, 66.0), 0, -10, -1, TRUE, 0, TRUE, 1.5);
    Case("FAR", "td_rogue", Chest(70.0, 66.0), 0, -10, -1, TRUE, 0, TRUE, 9.0);
    for (i = 0; i < 12; i++) Fight("FIGHT" + IntToString(i), 10.0 + 6.0 * IntToFloat(i % 6), 72.0 + 4.0 * IntToFloat(i / 6));
    for (i = 0; i < 12; i++) DelayCommand(3.0, FightGo("FIGHT" + IntToString(i)));
}

void Report2()
{
    FightReport();
    string s = "FLAGK5 FLAGK6 FLAGK7 FLAGK8 RECK22 RECK23 ROGUE36 ROGUE60 ROGUEOK60 ROGUEDEADLY ROGUEFLAG36 ROGUEREC36 NEAR FAR ";
    while (GetStringLength(s) > 0)
    {
        int n = FindSubString(s, " ");
        Result(GetStringLeft(s, n));
        s = GetStringRight(s, GetStringLength(s) - n - 1);
    }
    object m = GetModule();
    DeleteLocalInt(m, "PTEV_N");
}

void Report()
{
    string s = "SUCC FAILBIG FAILSMALL FAIL4 FAIL5 EXACT STAT0 STAT1 STAT2 STAT3 STAT4 STAT5 FLAG FLAGA FLAGB FLAGBIG EXAM EXAMA EXAMB EXAMBIG REC RECA RECB RECFAIL NOREC NODIS UNDET ROGUE35 ROGUE34 ROGUEFLAG35 ROGUEEXAM35 UNTRAINED FIGHTER TRIG TRIGREC TRIGFAIL DOOR DOORFAIL ";
    while (GetStringLength(s) > 0)
    {
        int n = FindSubString(s, " ");
        Result(GetStringLeft(s, n));
        s = GetStringRight(s, GetStringLength(s) - n - 1);
    }
    object m = GetModule();
    DeleteLocalInt(m, "PTEV_N");
}

void main()
{
    PStart();
    DelayCommand(1.0, Batch1());
    DelayCommand(12.0, Report());
    DelayCommand(13.0, Batch2());
    DelayCommand(30.0, Report2());
    DelayCommand(31.0, PDone());
}
