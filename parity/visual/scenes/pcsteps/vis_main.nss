// Combat steps of a player's character against its NPC copy (CopyObject), each fighting a passive bandit with a
// dagger: with one attack a round, with two, with six, and with one from 0.5 m (too close). Both positions are
// logged every tenth of a second on a 20 Hz script clock, with each round's end and each attack.
#include "visual_inc"
#include "cadence_inc"

void Quiet(object o) { int i; for (i = 5000; i <= 5012; i++) SetEventScript(o, i, ""); SetAILevel(o, AI_LEVEL_NORMAL); SetPlotFlag(o, TRUE); SetSoundset(o, 65535); AssignCommand(o, ClearAllActions(TRUE)); }

string P(object o) { vector v = GetPosition(o); return FloatToString(v.x, 0, 3) + "," + FloatToString(v.y, 0, 3) + "," + FloatToString(GetFacing(o), 0, 1); }

void Pos(object pc, object clone)
{
    CNote("pos", IntToString(GetLocalInt(GetModule(), "cadence_clock")) + "|" + GetLocalString(GetModule(), "cadence_phase")
        + "|pc=" + P(pc) + "|copy=" + P(clone) + "|pct=" + P(GetLocalObject(pc, "cadence_target")) + "|copyt=" + P(GetLocalObject(clone, "cadence_target")));
    if (!GetLocalInt(GetModule(), "steps_done")) DelayCommand(0.1, Pos(pc, clone));
}

object Pair(object pc, object actor, string key, float x, float y)
{
    object target = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(x, y + 1.5, 0.0), 270.0));
    Quiet(actor); Quiet(target); SetCommandable(FALSE, target);
    SetLocalString(actor, "cadence_key", key); SetLocalObject(target, "cadence_attacker", actor); SetLocalObject(actor, "cadence_target", target);
    SetLocalFloat(actor, "home_x", x); SetLocalFloat(actor, "home_y", y);
    SetEventScript(actor, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, "cad_round"); SetEventScript(target, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "cad_atk");
    SetIsTemporaryEnemy(actor, target); SetIsTemporaryEnemy(target, actor);
    object weapon = GetItemPossessedBy(actor, "NW_WSWDG001"); AssignCommand(actor, ActionEquipItem(weapon, INVENTORY_SLOT_RIGHTHAND));
    return target;
}

void Home(object a, float fDy)
{
    AssignCommand(a, ClearAllActions(TRUE));
    AssignCommand(a, JumpToLocation(Location(GetArea(a), Vector(GetLocalFloat(a, "home_x"), GetLocalFloat(a, "home_y") + fDy, 0.0), 90.0)));
}

void Start(object a, int count)
{
    SetBaseAttackBonus(count, a);
    CNote(GetLocalString(GetModule(), "cadence_phase") + "." + GetLocalString(a, "cadence_key") + ".apr", IntToString(GetAttacksPerRound(a)));
    AssignCommand(a, ActionAttack(GetLocalObject(a, "cadence_target")));
}

void Phase(object pc, object clone, string name, int count)
{
    SetLocalString(GetModule(), "cadence_phase", name);
    Start(pc, count); Start(clone, count);
}

void Stop(object pc, object clone, float fDy)
{
    SetLocalString(GetModule(), "cadence_phase", "reset");
    Home(pc, fDy); Home(clone, fDy);
}

void Done() { SetLocalInt(GetModule(), "steps_done", 1); VDone(); }

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "cadence_started")) return; SetLocalInt(GetModule(), "cadence_started", 1);
    CClockStart(); SetCameraMode(pc, CAMERA_MODE_TOP_DOWN); SetPlotFlag(pc, TRUE);
    object clone = CopyObject(pc, Location(GetArea(pc), Vector(40.0, 30.0, 0.0), 90.0), OBJECT_INVALID, "cadence_copy");
    CNote("copy.valid", IntToString(GetIsObjectValid(clone))); if (!GetIsObjectValid(clone)) { VDone(); return; }
    Pair(pc, pc, "pc", 30.0, 30.0); Pair(pc, clone, "copy", 40.0, 30.0);
    DelayCommand(1.0, VCamera(pc, 90.0, 20.0, 75.0));
    DelayCommand(2.0, Pos(pc, clone));
    DelayCommand(9.0, Phase(pc, clone, "n1", 1));     DelayCommand(45.0, Stop(pc, clone, 0.0));
    DelayCommand(49.0, Phase(pc, clone, "n2", 2));    DelayCommand(85.0, Stop(pc, clone, 0.0));
    DelayCommand(89.0, Phase(pc, clone, "n6", 6));    DelayCommand(125.0, Stop(pc, clone, 1.0));
    DelayCommand(129.0, Phase(pc, clone, "close", 1)); DelayCommand(165.0, Stop(pc, clone, 0.0));
    DelayCommand(12.0, VShot("one_attack")); DelayCommand(52.0, VShot("two_attacks")); DelayCommand(92.0, VShot("six_attacks"));
    DelayCommand(132.0, VShot("close"));
    DelayCommand(168.0, Done());
}
