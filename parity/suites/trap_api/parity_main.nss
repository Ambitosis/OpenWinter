// Trap script functions: what CreateTrapAtLocation / CreateTrapOnObject make, toolset traps, every getter
// and setter (also on objects without a trap), detection bookkeeping, SetTrapDisabled and OnDisarm.
#include "ptrap_inc"

void Make(int nType, location l, float fSize, string sTag, int nFaction, string sDisarm, string sTrig)
{
    object t = CreateTrapAtLocation(nType, l, fSize, sTag, nFaction, sDisarm, sTrig);
    SetLocalObject(GetModule(), "MADE_" + sTag, t);
}

void MakeOn(int nType, object o, int nFaction, string sDisarm, string sTrig)
{
    CreateTrapOnObject(nType, o, nFaction, sDisarm, sTrig);
}

object Actor(string sTag, float x, float y, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PTrapL(x, y), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, nFaction);
    return o;
}

void Phase1()
{
    object m = GetModule();
    object a = GetLocalObject(m, "MADE_T_A");
    PTrapProps("made", a);
    PLog("made.tag", GetTag(a));
    PLog("made.name", GetName(a));
    PLogI("made.isself", a == GetObjectByTag("T_A"));
    vector p = GetPosition(a);
    PLogF("made.x", p.x, 0.01); PLogF("made.y", p.y, 0.01); PLogF("made.z", p.z, 0.01);
    PLogF("made.facing", GetFacing(a), 0.5);
    PLogI("made.factionhostile", GetFactionEqual(a, GetObjectByTag("HOSTILE")));
    PLogI("made.factioncommoner", GetFactionEqual(a, GetObjectByTag("COMMONER")));

    object b = GetLocalObject(m, "MADE_");
    PTrapProps("module", b);
    PLog("module.tag", GetTag(b));
    PLog("module.name", GetName(b));
    PLogI("module.factionhostile", GetFactionEqual(b, GetObjectByTag("HOSTILE")));
    PLogI("module.factioncommoner", GetFactionEqual(b, GetObjectByTag("COMMONER")));

    PTrapProps("mod2", GetLocalObject(m, "MADE_MOD2"));
    PTrapProps("mod05", GetLocalObject(m, "MADE_MOD05"));
    PLogI("mod05.size1.valid", GetIsObjectValid(GetLocalObject(m, "MADE_MOD10")));
    PLogI("modhostile.valid", GetIsObjectValid(GetLocalObject(m, "MADE_MODH")));
    object c = GetLocalObject(m, "MADE_T_BAD");
    PTrapProps("badtype", c);

    PTrapProps("chest", GetObjectByTag("CHEST"));
    PTrapProps("plain", GetObjectByTag("PLAIN"));
    PTrapProps("creature", GetObjectByTag("COMMONER"));
    PTrapProps("git", GetObjectByTag("GIT_TRAP"));
    PLog("git.name", GetName(GetObjectByTag("GIT_TRAP")));
    PTrapProps("gitdoor", GetObjectByTag("GIT_DOOR"));
    PTrapProps("gitchest", GetObjectByTag("GIT_CHEST"));
    PTrapProps("invalid", OBJECT_INVALID);

    // Setters on the trap trigger.
    SetTrapDetectDC(a, 300); PLogI("set.detectdc300", GetTrapDetectDC(a));
    SetTrapDetectDC(a, -5); PLogI("set.detectdcneg", GetTrapDetectDC(a));
    SetTrapDetectDC(a, 17); PLogI("set.detectdc17", GetTrapDetectDC(a));
    SetTrapDisarmDC(a, 251); PLogI("set.disarmdc251", GetTrapDisarmDC(a));
    SetTrapDisarmDC(a, 19); PLogI("set.disarmdc19", GetTrapDisarmDC(a));
    SetTrapKeyTag(a, "TRAPKEY"); PLog("set.keytag", GetTrapKeyTag(a));
    SetTrapOneShot(a, FALSE); PLogI("set.oneshot0", GetTrapOneShot(a));
    SetTrapRecoverable(a, FALSE); PLogI("set.recoverable0", GetTrapRecoverable(a));
    SetTrapDisarmable(a, FALSE); PLogI("set.disarmable0", GetTrapDisarmable(a));
    SetTrapDetectable(a, FALSE); PLogI("set.detectable0", GetTrapDetectable(a));
    SetTrapActive(a, FALSE); PLogI("set.active0", GetTrapActive(a)); PLogI("set.active0.trapped", GetIsTrapped(a));
    SetTrapActive(a, TRUE); PLogI("set.active1", GetTrapActive(a));
    SetTrapOneShot(a, TRUE); SetTrapRecoverable(a, TRUE); SetTrapDisarmable(a, TRUE); SetTrapDetectable(a, TRUE);

    // Setters on an object without a trap.
    object plain = GetObjectByTag("PLAIN");
    SetTrapDetectDC(plain, 33); SetTrapDisarmDC(plain, 34); SetTrapKeyTag(plain, "PK"); SetTrapOneShot(plain, FALSE);
    SetTrapActive(plain, FALSE); SetTrapDetectable(plain, FALSE);
    PTrapProps("plainset", plain);

    // Detection bookkeeping.
    object com = GetObjectByTag("COMMONER");
    PLogI("det.before", GetTrapDetectedBy(a, com));
    PLogI("det.creator", GetTrapDetectedBy(a, GetObjectByTag("MAKER")));
    PLog("det.nearest0", PTrapTag(GetNearestTrapToObject(com, FALSE)));
    PLog("det.nearest1.before", PTrapTag(GetNearestTrapToObject(com, TRUE)));
    PLog("det.last.before", PTrapTag(GetLastTrapDetected(com)));
    PLogI("det.set", SetTrapDetectedBy(a, com, TRUE));
    PLogI("det.after", GetTrapDetectedBy(a, com));
    PLogI("det.setagain", SetTrapDetectedBy(a, com, TRUE));
    PLog("det.nearest1", PTrapTag(GetNearestTrapToObject(com, TRUE)));
    PLog("det.last", PTrapTag(GetLastTrapDetected(com)));
    PLogI("det.setplain", SetTrapDetectedBy(plain, com, TRUE));
    PLogI("det.getplain", GetTrapDetectedBy(plain, com));
    PLogI("det.setbyplc", SetTrapDetectedBy(a, plain, TRUE));
    PLogI("det.getbyplc", GetTrapDetectedBy(a, plain));
    PLogI("det.clear", SetTrapDetectedBy(a, com, FALSE));
    PLogI("det.cleared", GetTrapDetectedBy(a, com));
    PLogI("det.clearagain", SetTrapDetectedBy(a, com, FALSE));
    PLog("det.last.cleared", PTrapTag(GetLastTrapDetected(com)));
    PLog("det.nearest1.cleared", PTrapTag(GetNearestTrapToObject(com, TRUE)));
    PLogI("det.inactive.set", SetTrapDetectedBy(GetObjectByTag("GIT_TRAP"), com, TRUE));
    PLogI("det.inactive.get", GetTrapDetectedBy(GetObjectByTag("GIT_TRAP"), com));
    PLogI("det.module", GetTrapDetectedBy(b, com));

    // Re-trapping a trapped chest.
    object chest = GetObjectByTag("CHEST");
    CreateTrapOnObject(TRAP_BASE_TYPE_DEADLY_FIRE, chest, STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    PTrapProps("retrap", chest);
    PTrapEvents("phase1");
}

void Phase2()
{
    object m = GetModule();
    object a = GetLocalObject(m, "MADE_T_A");
    SetTrapDisabled(a);
    PLogI("disabled.valid0", GetIsObjectValid(a));
    PLogI("disabled.trapped0", GetIsTrapped(a));
    PTrapEvents("disabled.now");
    DelayCommand(0.0, PLogI("disabled.d0.valid", GetIsObjectValid(a)));
    DelayCommand(0.0, PLogI("disabled.d0.trapped", GetIsTrapped(a)));
    DelayCommand(0.0, PTrapEvents("disabled.d0"));
    DelayCommand(0.1, PLogI("disabled.d01.valid", GetIsObjectValid(a)));
    DelayCommand(0.1, PTrapEvents("disabled.d01"));
    DelayCommand(0.5, PLogI("disabled.d05.valid", GetIsObjectValid(a)));
    DelayCommand(0.5, PTrapEvents("disabled.d05"));
    object chest = GetObjectByTag("CHEST");
    SetTrapDisabled(chest);
    PLogI("chestdisabled.valid0", GetIsObjectValid(chest));
    PLogI("chestdisabled.trapped0", GetIsTrapped(chest));
    PTrapEvents("chestdisabled.now");
    SetTrapDisabled(GetObjectByTag("PLAIN"));
    PTrapEvents("plaindisabled.now");
}

void Phase3()
{
    object m = GetModule();
    object a = GetLocalObject(m, "MADE_T_A");
    PLogI("disabled.valid1", GetIsObjectValid(a));
    PTrapProps("disabled.later", a);
    PTrapProps("chestdisabled.later", GetObjectByTag("CHEST"));
    PTrapEvents("phase3");
    DestroyObject(GetObjectByTag("MAKER"));
    DelayCommand(1.0, PLog("creatorgone", PTrapTag(GetTrapCreator(GetObjectByTag("MADE_CHEST")))));
}

void main()
{
    object maker = Actor("MAKER", 10.0, 10.0, STANDARD_FACTION_COMMONER);
    Actor("COMMONER", 30.0, 10.0, STANDARD_FACTION_COMMONER);
    Actor("HOSTILE", 30.0, 30.0, STANDARD_FACTION_HOSTILE);
    object chest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(14.0, 30.0), FALSE, "CHEST");
    CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(16.0, 30.0), FALSE, "PLAIN");
    object mc = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(18.0, 30.0), FALSE, "MADE_CHEST");
    AssignCommand(maker, Make(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(20.0, 20.0, 45.0), 3.0, "T_A", STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig"));
    Make(TRAP_BASE_TYPE_AVERAGE_GAS, PTrapL(26.0, 20.0), 0.5, "", STANDARD_FACTION_COMMONER, "", "");
    Make(TRAP_BASE_TYPE_AVERAGE_GAS, PTrapL(26.0, 26.0), 2.0, "MOD2", STANDARD_FACTION_COMMONER, "", "");
    Make(TRAP_BASE_TYPE_AVERAGE_GAS, PTrapL(26.0, 32.0), 0.5, "MOD05", STANDARD_FACTION_HOSTILE, "", "");
    Make(TRAP_BASE_TYPE_AVERAGE_GAS, PTrapL(26.0, 38.0), 1.0, "MOD10", STANDARD_FACTION_HOSTILE, "", "");
    Make(TRAP_BASE_TYPE_AVERAGE_GAS, PTrapL(26.0, 44.0), 2.0, "MODH", STANDARD_FACTION_HOSTILE, "", "");
    Make(99, PTrapL(32.0, 20.0), 2.0, "T_BAD", STANDARD_FACTION_HOSTILE, "", "");
    CreateTrapOnObject(TRAP_BASE_TYPE_STRONG_ACID, chest, STANDARD_FACTION_MERCHANT, "tp_disarm", "tp_trig");
    AssignCommand(maker, MakeOn(TRAP_BASE_TYPE_MINOR_FROST, mc, STANDARD_FACTION_HOSTILE, "", ""));
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_FROST, GetObjectByTag("COMMONER"), STANDARD_FACTION_HOSTILE, "", "");
    DelayCommand(1.0, PTrapProps("madechest", mc));
    DelayCommand(1.5, Phase1());
    DelayCommand(3.0, Phase2());
    DelayCommand(5.0, Phase3());
    DelayCommand(8.0, PDone());
}
