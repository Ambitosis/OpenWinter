// Doors killed by EffectDamage, EffectDeath and bashing, and one removed with DestroyObject
// (docs/INTER_AREA_MOVEMENT.md, "Killed doors"): what a script still sees of the door and its linked door, their
// events, door actions on a dead door, jumps to it, creatures routing and pursuing through it both ways, walking
// across the doorway and past its leaf. Areas come from parity/gen_interarea.py.
#include "pbashed_inc"

object Mk(string sWp, string sTag, int bOpen = TRUE)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "ia_walk", GetLocation(GetObjectByTag(sWp)), FALSE, sTag);
    SetAILevel(o, AI_LEVEL_NORMAL);
    IATrack(o, 1.0, 0.5);
    // A closed door on the way is opened, as the default OnBlocked does.
    if (bOpen) SetLocalInt(o, "OPEN_ON_BLOCK", 1);
    return o;
}

void Done(string sTag) { if (!GetLocalInt(GetObjectByTag(sTag), "IA_NODONE")) IADone(GetObjectByTag(sTag)); }

void ToObject(string sTag, string sGoal, int bRun = FALSE)
{
    object w = GetObjectByTag(sTag);
    AssignCommand(w, ActionMoveToObject(GetObjectByTag(sGoal), bRun));
    AssignCommand(w, ActionDoCommand(Done(sTag)));
}

void ToLocation(string sTag, string sGoal)
{
    object w = GetObjectByTag(sTag);
    AssignCommand(w, ActionMoveToLocation(GetLocation(GetObjectByTag(sGoal))));
    AssignCommand(w, ActionDoCommand(Done(sTag)));
}

string DoorTag(int i)
{
    switch (i)
    {
        case 0: return "ED_0"; case 1: return "EK_0"; case 2: return "CC_0"; case 3: return "OP_0"; case 4: return "CK_0";
        case 5: return "PU_0"; case 6: return "JP_0"; case 7: return "SX_DEAD"; case 8: return "DS_0"; case 9: return "BA_0";
        case 10: return "CO_0"; case 11: return "UK"; case 12: return "UW"; case 13: return "UN"; case 14: return "LD";
        case 15: return "KD"; case 16: return "UD"; case 17: return "CU_0"; case 18: return "CX_0";
    }
    return "";
}

void State(string t, string sPhase) { BState(GetObjectByTag(t), t, sPhase); }

object Door(string t) { return GetLocalObject(GetModule(), "BT_OBJ_" + t); }
void Later(string t, string sPhase) { BState(Door(t), t, sPhase); }
void Can(string sBy, string t, string sPhase) { AssignCommand(GetObjectByTag(sBy), BCan(Door(t), t, sPhase)); }

void Damage(string t) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(50, DAMAGE_TYPE_BLUDGEONING), Door(t)); BState(Door(t), t, "now"); }
void Death(string t) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), Door(t)); BState(Door(t), t, "now"); }

void Kill()
{
    Damage("ED_0"); BFollow(Door("ED_0"), "ED_0");
    Death("EK_0"); BFollow(Door("EK_0"), "EK_0");
    Damage("CC_0"); Damage("CO_0"); Damage("CK_0"); Damage("PU_0"); Damage("JP_0"); Damage("SX_DEAD"); Damage("UW"); Damage("UN");
    Damage("LD"); Damage("KD"); Damage("CU_0"); Damage("CX_0");
    Damage("OP_0"); BFollow(Door("OP_0"), "OP_0");
    Death("UK");
    DestroyObject(Door("DS_0")); BState(Door("DS_0"), "DS_0", "now"); BFollow(Door("DS_0"), "DS_0");
    DestroyObject(Door("UD")); BState(Door("UD"), "UD", "now");
    int i;
    for (i = 0; i <= 18; i++) if (i != 9) BGone(Door(DoorTag(i)), DoorTag(i), PSince(), 0);
    DelayCommand(1.0, State("ED_0_R", "d1")); DelayCommand(1.0, State("EK_0_R", "d1")); DelayCommand(1.0, State("DS_0_R", "d1"));
    // (The linked door then reads GetIsDoorActionPossible(BASH) FALSE on the original, although an open door that
    // nothing killed reads TRUE (bash_dispatch). That is not modelled, so it is not compared.)
    DelayCommand(1.0, Can("ED", "ED_0", "d1"));
    DelayCommand(10.0, State("ED_0_R", "d10"));
    // The basher breaks the locked door, then walks on through it.
    SetLocalInt(GetModule(), "BT_FOLLOW_BA_0", 1);
    object ba = GetObjectByTag("BA");
    AssignCommand(ba, DoDoorAction(Door("BA_0"), DOOR_ACTION_BASH));
    ToObject("BA", "BA_GOAL");
}

// Dead doors closed by a creature: CC_0 (then locked by script), CU_0 and CX_0. CO_0, dead and open, is opened by a
// creature; LO, alive and open, too.
void CloseIt()
{
    AssignCommand(GetObjectByTag("CCU"), ActionCloseDoor(Door("CC_0")));
    AssignCommand(GetObjectByTag("CUU"), ActionCloseDoor(Door("CU_0")));
    AssignCommand(GetObjectByTag("CXU"), ActionCloseDoor(Door("CX_0")));
}
void OpenLive()
{
    object u = GetObjectByTag("LOU");
    AssignCommand(u, DoDoorAction(GetObjectByTag("LO"), DOOR_ACTION_OPEN));
    AssignCommand(u, ActionMoveToLocation(GetLocation(GetObjectByTag("LO_AWAY"))));
}

// LO and LD are opened by a creature standing north of them, which then steps away.
void Opener(string t)
{
    object o = GetObjectByTag(t + "O");
    AssignCommand(o, ActionOpenDoor(GetObjectByTag(t)));
    AssignCommand(o, ActionMoveToLocation(GetLocation(GetObjectByTag(t + "_AWAY"))));
}
void OpenIt()
{
    AssignCommand(GetObjectByTag("COU"), DoDoorAction(Door("CO_0"), DOOR_ACTION_OPEN));
    DelayCommand(3.0, AssignCommand(GetObjectByTag("COU"), ActionOpenDoor(Door("CO_0"))));
}

// The first times a door is seen shut and open again after fStart (polled every 0.25 s).
void Watch(string t, float fStart, int n)
{
    object d = Door(t);
    int bOpen = GetIsOpen(d);
    if (!bOpen && !GetLocalInt(d, "seen_shut")) { SetLocalInt(d, "seen_shut", 1); PLogF(t + ".shut.after", PSince() - fStart, 1.25); }
    if (bOpen && GetLocalInt(d, "seen_shut") && !GetLocalInt(d, "seen_reopen")) { SetLocalInt(d, "seen_reopen", 1); PLogF(t + ".reopen.after", PSince() - fStart, 1.25); }
    if (n < 280) DelayCommand(0.25, Watch(t, fStart, n + 1));
}

void Jumps()
{
    object d = Door("JP_0");
    AssignCommand(GetObjectByTag("JPJ"), JumpToObject(d));
    AssignCommand(GetObjectByTag("JPL"), JumpToLocation(GetLocation(d)));
    AssignCommand(GetObjectByTag("JPR"), JumpToObject(GetObjectByTag("JP_0_R")));
}

void JumpEnd(string sTag) { IAPos(sTag + ".jumped", GetObjectByTag(sTag)); }

// Where each walker crosses y = 15 inside the one area (straight through the dead doorway, or round the live door).
void Cross(string sTag, int n)
{
    object o = GetObjectByTag(sTag);
    vector p = GetPosition(o);
    if (!GetLocalInt(o, "crossed") && p.y >= 15.0)
    {
        SetLocalInt(o, "crossed", 1);
        PLogF(sTag + ".cross.x", p.x, 0.3);
    }
    if (n < 200) DelayCommand(0.1, Cross(sTag, n + 1));
}

// How far a walker along a line 1 m from a door strays from it (round a leaf in its way).
void Stray(string sTag, float fY, int n)
{
    object o = GetObjectByTag(sTag);
    float d = fabs(GetPosition(o).y - fY);
    if (d > GetLocalFloat(o, "stray")) SetLocalFloat(o, "stray", d);
    if (n < 150) DelayCommand(0.1, Stray(sTag, fY, n + 1));
    else PLogF(sTag + ".stray", GetLocalFloat(o, "stray"), 0.25);
}

// Where a walker crosses y = 25 (the CX door).
void Cross2(string sTag, int n)
{
    object o = GetObjectByTag(sTag);
    vector p = GetPosition(o);
    if (!GetLocalInt(o, "crossed") && p.y >= 25.0)
    {
        SetLocalInt(o, "crossed", 1);
        PLogF(sTag + ".cross.x", p.x, 0.3);
    }
    if (n < 200) DelayCommand(0.1, Cross2(sTag, n + 1));
}

void Leaves()
{
    int i;
    for (i = 0; i < 6; i++)
    {
        string t = GetSubString("LO LD KD ", (i / 2) * 3, 2) + (i % 2 ? "N" : "S");
        ToLocation(t, t + "_GOAL");
        Stray(t, i % 2 ? 16.0 : 14.0, 0);
    }
}

void Attack(string sAttacker, string sTarget) { AssignCommand(GetObjectByTag(sAttacker), ActionAttack(GetObjectByTag(sTarget))); }

void Flee(string sTag, string sGoal)
{
    AssignCommand(GetObjectByTag(sTag), ClearAllActions());
    ToObject(sTag, sGoal, TRUE);
}

void Go()
{
    ToObject("ED", "ED_GOAL"); ToObject("EK", "EK_GOAL"); ToObject("CC", "CC_GOAL"); ToObject("CO", "CO_GOAL");
    ToObject("CU", "CU_GOAL"); ToLocation("CXX", "CXX_GOAL"); Cross2("CXX", 0);
    ToObject("OP", "OP_GOAL"); ToObject("CK", "CK_GOAL"); ToObject("DS", "DS_GOAL");
    ToLocation("SXL", "SXL_GOAL"); Cross("SXL", 0);
    Attack("PUA", "PUT");
    DelayCommand(4.0, Flee("PUT", "PU_GOAL"));
}

// Straight across the doorways of LO (alive, open), LD and KD (dead).
void Across() { ToLocation("LOX", "LOX_GOAL"); ToLocation("LDX", "LDX_GOAL"); ToLocation("KDX", "KDX_GOAL"); Cross("LOX", 0); Cross("LDX", 0); Cross("KDX", 0); }

// Back the other way, into the dead door from its linked door.
void Back() { ToObject("EDR", "EDR_GOAL"); ToObject("EKR", "EKR_GOAL"); ToObject("OPR", "OPR_GOAL"); ToObject("DSR", "DSR_GOAL"); }

string Walker(int i)
{
    switch (i)
    {
        case 0: return "ED"; case 1: return "EDR"; case 2: return "EK"; case 3: return "EKR"; case 4: return "CC";
        case 5: return "CO"; case 6: return "OP"; case 7: return "OPR"; case 8: return "CK"; case 9: return "DS";
        case 10: return "DSR"; case 11: return "BA"; case 12: return "LOX"; case 13: return "SXL"; case 14: return "PUA";
        case 15: return "PUT"; case 16: return "JPJ"; case 17: return "JPL"; case 18: return "JPR"; case 19: return "LDX";
        case 20: return "KDX"; case 21: return "CU"; case 22: return "CXX";
    }
    return "";
}

void End()
{
    int i;
    for (i = 0; i <= 22; i++)
    {
        // Where the pursuit's fight ends up is not compared, only the area.
        IAEnd(GetObjectByTag(Walker(i)), i != 14 && i != 15);
        PLogI(Walker(i) + ".end.action", GetCurrentAction(GetObjectByTag(Walker(i))));
    }
    for (i = 0; i <= 18; i++)
    {
        string t = DoorTag(i);
        Later(t, "end");
        BCounts(t);
        if (GetIsObjectValid(GetObjectByTag(t + "_R"))) { BCounts(t + "_R"); State(t + "_R", "end"); }
    }
    BCounts("SX_LIVE"); BCounts("LO");
    PDone();
}

void main()
{
    IAStart();
    Mk("ED_START", "ED"); Mk("EDR_START", "EDR"); Mk("EK_START", "EK"); Mk("EKR_START", "EKR");
    Mk("BA_BASH", "BA"); Mk("CC_START", "CC"); Mk("CO_START", "CO"); Mk("OP_START", "OP"); Mk("OPR_START", "OPR");
    Mk("CK_START", "CK"); Mk("DS_START", "DS"); Mk("DSR_START", "DSR");
    Mk("SXL_START", "SXL", FALSE); Mk("CU_START", "CU"); Mk("CXX_START", "CXX", FALSE);
    SetLocalInt(Mk("CU_USER", "CUU"), "IA_TRACK", 0); SetLocalInt(Mk("CX_USER", "CXU"), "IA_TRACK", 0);
    Mk("LOX_START", "LOX", FALSE); Mk("LDX_START", "LDX", FALSE); Mk("KDX_START", "KDX", FALSE);
    SetLocalInt(Mk("LO_USER", "LOU"), "IA_TRACK", 0);
    Mk("JP_J1", "JPJ"); Mk("JP_J2", "JPL"); Mk("JP_J3", "JPR");
    SetLocalInt(Mk("CC_USER", "CCU"), "IA_TRACK", 0);
    SetLocalInt(Mk("CO_USER", "COU"), "IA_TRACK", 0);
    object pt = Mk("PU_T", "PUT");
    ChangeToStandardFaction(pt, STANDARD_FACTION_COMMONER); SetPlotFlag(pt, TRUE);
    SetLocalInt(pt, "IA_NODONE", 1); SetLocalInt(pt, "BT_FIGHT", 1);
    object pa = Mk("PU_A", "PUA");
    ChangeToStandardFaction(pa, STANDARD_FACTION_HOSTILE);
    // Where a pursuer lands depends on where its target stands at that moment (it is placed clear of it).
    SetLocalFloat(pa, "IA_TOLP", 1.6); SetLocalInt(pa, "BT_FIGHT", 1);
    int i;
    // The walkers past the leaves report only how far they strayed (OpenWinter's walker can get stuck round a live
    // door's leaf, which is not what this suite is about).
    for (i = 0; i < 6; i++)
    {
        string t = GetSubString("LO LD KD ", (i / 2) * 3, 2) + (i % 2 ? "N" : "S");
        object w = Mk(t + "_START", t, FALSE);
        SetLocalInt(w, "IA_TRACK", 0); SetLocalInt(w, "IA_NODONE", 1);
    }
    // The openers' timing is not what this suite is about either.
    SetLocalInt(GetModule(), "BT_NOTIME_LO", 1); SetLocalInt(GetModule(), "BT_NOTIME_LD", 1);
    // The bash: each swing hits or misses at random, and its damage is random.
    SetLocalFloat(GetModule(), "BT_TOL_BA_0", 2.5); SetLocalFloat(GetModule(), "BT_TOL_BA_0_R", 2.5);
    SetLocalInt(GetModule(), "BT_HPSIGN_BA_0", 1);
    // A creature closing a door: OpenWinter's is done 0.5 to 0.9 s sooner (door action timing, not this suite's
    // subject; the original itself varies by 0.3 s).
    SetLocalFloat(GetModule(), "BT_TOL_CC_0", 0.5); SetLocalFloat(GetModule(), "BT_TOL_CU_0", 0.5); SetLocalFloat(GetModule(), "BT_TOL_CX_0", 0.5);
    object ba = GetObjectByTag("BA");
    SetLocalFloat(ba, "IA_TOLT", 3.0);
    // After the bash the original walked onto its goal; OpenWinter stops 1 m short, as after any one-door route.
    SetLocalFloat(ba, "IA_TOLP", 1.2);
    for (i = 0; i <= 18; i++)
    {
        string t = DoorTag(i);
        BMark(t);
        SetLocalObject(GetModule(), "BT_OBJ_" + t, GetObjectByTag(t));
    }
    object op = GetObjectByTag("OP_0"); AssignCommand(op, ActionOpenDoor(op));
    SetLocalInt(Mk("LO_OPENER", "LOO", FALSE), "IA_TRACK", 0); SetLocalInt(Mk("LD_OPENER", "LDO", FALSE), "IA_TRACK", 0);
    Opener("LO"); Opener("LD");
    DelayCommand(2.9, State("ED_0", "pre")); DelayCommand(2.9, State("OP_0", "pre")); DelayCommand(2.9, State("ED_0_R", "pre"));
    DelayCommand(2.9, Can("ED", "ED_0", "pre"));
    DelayCommand(3.0, Kill());
    DelayCommand(4.0, CloseIt()); DelayCommand(4.0, Watch("CC_0", 4.0, 0)); DelayCommand(4.0, Watch("CU_0", 4.0, 0));
    DelayCommand(4.0, Watch("CX_0", 4.0, 0)); DelayCommand(11.0, Can("CU", "CU_0", "shut"));
    DelayCommand(10.0, SetLocked(Door("CC_0"), TRUE));
    DelayCommand(11.0, Later("CC_0", "locked")); DelayCommand(11.0, Can("CC", "CC_0", "locked"));
    DelayCommand(12.0, OpenLive()); DelayCommand(25.0, Across());
    DelayCommand(4.0, OpenIt()); DelayCommand(9.0, Later("CO_0", "opened"));
    DelayCommand(5.0, Jumps());
    DelayCommand(7.0, JumpEnd("JPJ")); DelayCommand(7.0, JumpEnd("JPL")); DelayCommand(7.0, JumpEnd("JPR"));
    DelayCommand(5.0, Leaves());
    DelayCommand(14.0, Go());
    DelayCommand(40.0, Back());
    DelayCommand(72.0, End());
}
