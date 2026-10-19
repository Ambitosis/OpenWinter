// Set Trap with trap kits: ActionUseSkill on a chest or on oneself, ActionUseItemOnObject and
// ActionUseItemAtLocation; the trap made (type, DCs, faction, creator, who knows about it, its shape),
// the kit used up, success and failure, and how long it takes.
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

// route 0: ActionUseSkill on the target, 1: ActionUseItemOnObject, 2: ActionUseItemAtLocation (the target's spot),
// 3: ActionUseSkill on oneself.
void Case(string k, string sRes, string sKit, int nRoute, int nChest, float x, float y, int nPenalty = 0)
{
    object a = Actor("A_" + k, sRes, x - 3.0, y);
    if (nPenalty > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillDecrease(SKILL_SET_TRAP, nPenalty), a);
    object kit = CreateItemOnObject(sKit, a, 2);
    object t = a;
    if (nChest) t = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(x, y), FALSE, "C_" + k);
    PLogI(k + ".rank", GetSkillRank(SKILL_SET_TRAP, a));
    SetLocalObject(a, "KIT", kit);
    SetLocalObject(a, "TARGET", t);
    SetLocalFloat(a, "T0", PSince());
    itemproperty ip = GetFirstItemProperty(kit);
    if (nRoute == 0) AssignCommand(a, ActionUseSkill(SKILL_SET_TRAP, t, 0, kit));
    if (nRoute == 1) AssignCommand(a, ActionUseItemOnObject(kit, ip, t));
    if (nRoute == 2) AssignCommand(a, ActionUseItemAtLocation(kit, ip, PTrapL(x, y, 30.0)));
    if (nRoute == 3) AssignCommand(a, ActionUseSkill(SKILL_SET_TRAP, a, 0, kit));
    AssignCommand(a, ActionDoCommand(SetLocalFloat(a, "DONE", PSince() - GetLocalFloat(a, "T0"))));
}

void Result(string k)
{
    object a = GetObjectByTag("A_" + k);
    object t = GetLocalObject(a, "TARGET");
    object kit = GetLocalObject(a, "KIT");
    PLogI(k + ".kit.valid", GetIsObjectValid(kit));
    PLogI(k + ".kit.stack", GetItemStackSize(kit));
    PLogF(k + ".done", GetLocalFloat(a, "DONE"), 0.4);
    if (t == a)
    {
        t = GetNearestTrapToObject(a, FALSE);
        PLogI(k + ".trap.objtype", GetObjectType(t));
        if (GetIsObjectValid(t))
        {
            vector p = GetPosition(t) - GetPosition(a);
            PLogF(k + ".trap.dx", p.x, 0.3);
            PLogF(k + ".trap.dy", p.y, 0.3);
            PLogF(k + ".trap.facing", GetFacing(t), 1.0);
            PLogF(k + ".trap.dist", GetDistanceBetween(a, t), 0.3);
            PLog(k + ".trap.tag", GetTag(t));
            PLog(k + ".trap.name", GetName(t));
        }
    }
    PTrapProps(k, t);
    PLogI(k + ".detected.setter", GetTrapDetectedBy(t, a));
    PLogI(k + ".detected.other", GetTrapDetectedBy(t, GetObjectByTag("OTHER")));
    PLogI(k + ".faction.setter", GetFactionEqual(t, a));
    PLogI(k + ".faction.hostile", GetFactionEqual(t, GetObjectByTag("HOSTILE")));
    PLog(k + ".last", PTrapTag(GetLastTrapDetected(a)));
    PLogI(k + ".hurt", GetCurrentHitPoints(a) < GetMaxHitPoints(a));
}

void main()
{
    PStart();
    Actor("OTHER", "ts_rogue", 5.0, 5.0);
    object h = Actor("HOSTILE", "ts_rogue", 5.0, 75.0);
    ChangeToStandardFaction(h, STANDARD_FACTION_HOSTILE);
    Case("SKILLCHEST", "ts_rogue", "nw_it_trap001", 0, TRUE, 10.0, 10.0);
    Case("ITEMCHEST", "ts_rogue", "nw_it_trap001", 1, TRUE, 30.0, 10.0);
    Case("ITEMLOC", "ts_rogue", "nw_it_trap001", 2, FALSE, 50.0, 10.0);
    Case("SKILLSELF", "ts_rogue", "nw_it_trap001", 3, FALSE, 70.0, 10.0);
    Case("NOSYN", "ts_nosyn", "nw_it_trap001", 1, TRUE, 10.0, 30.0);
    Case("FIRE", "ts_rogue", "nw_it_trap017", 1, TRUE, 30.0, 30.0);
    Case("DEADLY", "ts_rogue", "nw_it_trap004", 1, TRUE, 50.0, 30.0);
    Case("FAIL", "ts_rogue", "nw_it_trap001", 1, TRUE, 70.0, 30.0, 40);
    Case("UNTRAINED", "ts_untrained", "nw_it_trap001", 1, TRUE, 10.0, 50.0);
    Case("LOCFAIL", "ts_rogue", "nw_it_trap001", 2, FALSE, 30.0, 46.0, 40);
    Case("ITEMLOCFIRE", "ts_rogue", "nw_it_trap017", 2, FALSE, 50.0, 50.0);
    Case("HIGH", "ts_rogue", "nw_it_trap001", 1, TRUE, 70.0, 50.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SET_TRAP, 20), GetObjectByTag("A_HIGH"));
    Case("TAKE20", "ts_rogue", "nw_it_trap004", 1, TRUE, 50.0, 70.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SET_TRAP, 3), GetObjectByTag("A_TAKE20"));
    Case("TAKE20B", "ts_rogue", "nw_it_trap004", 1, TRUE, 70.0, 70.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SET_TRAP, 2), GetObjectByTag("A_TAKE20B"));
    Case("AGAIN", "ts_rogue", "nw_it_trap017", 1, TRUE, 10.0, 70.0);
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, GetObjectByTag("C_AGAIN"), STANDARD_FACTION_HOSTILE);
    Case("DOOR", "ts_rogue", "nw_it_trap001", 1, FALSE, 30.0, 70.0);
    object d = GetObjectByTag("T_DOOR");
    SetLocalObject(GetObjectByTag("A_DOOR"), "TARGET", d);
    AssignCommand(GetObjectByTag("A_DOOR"), ClearAllActions());
    AssignCommand(GetObjectByTag("A_DOOR"), ActionUseItemOnObject(GetLocalObject(GetObjectByTag("A_DOOR"), "KIT"), GetFirstItemProperty(GetLocalObject(GetObjectByTag("A_DOOR"), "KIT")), d));
    AssignCommand(GetObjectByTag("A_DOOR"), ActionDoCommand(SetLocalFloat(GetObjectByTag("A_DOOR"), "DONE", PSince())));
    DelayCommand(15.0, Result("SKILLCHEST"));
    DelayCommand(15.0, Result("ITEMCHEST"));
    DelayCommand(15.0, Result("ITEMLOC"));
    DelayCommand(15.0, Result("SKILLSELF"));
    DelayCommand(15.0, Result("NOSYN"));
    DelayCommand(15.0, Result("FIRE"));
    DelayCommand(15.0, Result("DEADLY"));
    DelayCommand(15.0, Result("FAIL"));
    DelayCommand(15.0, Result("UNTRAINED"));
    DelayCommand(15.0, Result("LOCFAIL"));
    DelayCommand(15.0, Result("ITEMLOCFIRE"));
    DelayCommand(15.0, Result("HIGH"));
    DelayCommand(15.0, Result("AGAIN"));
    DelayCommand(15.0, Result("TAKE20"));
    DelayCommand(15.0, Result("TAKE20B"));
    DelayCommand(15.0, Result("DOOR"));
    DelayCommand(15.5, PTrapEvents("all"));
    DelayCommand(16.0, PDone());
}
