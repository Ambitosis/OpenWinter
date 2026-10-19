#include "visual_inc"
// playerai (AI levels and attacks that need a player; docs/STOCK_AI.md):
// 1. AI levels with a player: hers, her henchman's, an NPC's and a placeable's in her area, an NPC's in an area
//    without players; then as she jumps to that area and back (levels every 0.5 s).
// 2. Her attack when its target jumps to another area: alone (ends?), and with a second enemy beside her (switch?).
//    Action, target and position every 0.1 s.
// 3. An encounter creature and a plain NPC both told ActionAttack(her, FALSE); she jumps to another area while they
//    close in (mid-transition), an area reachable through a transition trigger: does either follow her? Every 0.25 s.
// All the NPCs are script-less and immortal; she carries temporary hit points.

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|pai|" + s); }
object M() { return GetModule(); }
object O(string s) { return GetLocalObject(M(), s); }
float T() { return IntToFloat(GetTimeSecond() * 1000 + GetTimeMillisecond()) / 1000.0; }
string Tag(object o) { return GetIsObjectValid(o) ? GetTag(GetArea(o)) : "-"; }
string Lv(object o) { return GetIsObjectValid(o) ? IntToString(GetAILevel(o)) : "x"; }
string Id(object o) { return GetIsObjectValid(o) ? GetTag(o) : "none"; }
string Pos(object o) { vector v = GetPosition(o); return FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2); }

void Strip(object o)
{
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    AssignCommand(o, ClearAllActions(TRUE));
    SetImmortal(o, TRUE);
}
object Make(string sTag, object oArea, float x, float y, float f, int bHostile)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(x, y, 0.0), f), FALSE, sTag);
    Strip(o);
    ChangeToStandardFaction(o, bHostile ? STANDARD_FACTION_HOSTILE : STANDARD_FACTION_MERCHANT);
    SetPlotFlag(o, FALSE);
    SetLocalObject(M(), sTag, o);
    return o;
}
void MakeV(string sTag, object oArea, float x, float y, float f, int bHostile) { Make(sTag, oArea, x, y, f, bHostile); }
location In(string sArea, float x, float y) { return Location(GetObjectByTag(sArea), Vector(x, y, 0.0), 0.0); }
// Area B is the Prelude's m0q1b; its door M0Q1B_M0Q1C is where a transition trigger in area A (at 60,20) leads.
location B(float dx, float dy)
{
    object d = GetObjectByTag("M0Q1B_M0Q1C");
    vector v = GetPosition(d);
    return Location(GetArea(d), Vector(v.x + dx, v.y + dy, v.z), 0.0);
}
void Jump(object o, location l) { AssignCommand(o, ClearAllActions(TRUE)); AssignCommand(o, JumpToLocation(l)); }

// ---- 1. AI levels
void Levels(int n)
{
    if (n <= 0) return;
    object pc = GetFirstPC();
    Note("ail|" + FloatToString(T(), 0, 2) + "|pcarea=" + Tag(pc) + "|pc=" + Lv(pc) + "|hench=" + Lv(O("PAI_H")) + "|n1=" + Lv(O("PAI_N1"))
        + "|plc=" + Lv(O("PAI_P")) + "|nb=" + Lv(O("PAI_NB")) + "|n1combat=" + IntToString(GetIsInCombat(O("PAI_N1"))));
    DelayCommand(0.5, Levels(n - 1));
}
void Setup()
{
    object pc = GetFirstPC();
    object a = GetArea(pc);
    Make("PAI_N1", a, 28.0, 24.0, 180.0, FALSE);
    object h = Make("PAI_H", a, 18.0, 22.0, 0.0, FALSE);
    AddHenchman(pc, h);
    object p = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", Location(a, Vector(24.0, 16.0, 0.0), 0.0), FALSE, "PAI_P");
    SetLocalObject(M(), "PAI_P", p);
    object nb = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", B(2.0, 2.0), FALSE, "PAI_NB");
    Strip(nb); ChangeToStandardFaction(nb, STANDARD_FACTION_MERCHANT); SetPlotFlag(nb, FALSE); SetLocalObject(M(), "PAI_NB", nb);
    Note("setup|hench_master=" + Id(GetMaster(h)) + "|assoc=" + IntToString(GetAssociateType(h)) + "|plc=" + IntToString(GetIsObjectValid(p)));
}

// ---- 2. her attack, the target leaves
void Sample(string sKey, int n)
{
    if (n <= 0) return;
    object pc = GetFirstPC();
    Note(sKey + "|" + FloatToString(T(), 0, 2) + "|act=" + IntToString(GetCurrentAction(pc)) + "|target=" + Id(GetAttackTarget(pc))
        + "|pos=" + Pos(pc) + "|combat=" + IntToString(GetIsInCombat(pc)));
    DelayCommand(0.1, Sample(sKey, n - 1));
}
void Attack(object t) { object pc = GetFirstPC(); AssignCommand(pc, ClearAllActions(TRUE)); AssignCommand(pc, ActionAttack(t)); }
void Leave(string sKey, object t) { Note(sKey + "|leave|" + FloatToString(T(), 0, 2)); Jump(t, B(-3.0, 3.0)); }
void Gone(string s) { DestroyObject(O(s)); }

// ---- 3. encounter creature and plain NPC, she jumps away mid-approach
void Chase(int n)
{
    if (n <= 0) return;
    object e = O("PAI_EC"), n2 = O("PAI_N2");
    Note("chase|" + FloatToString(T(), 0, 2) + "|pcarea=" + Tag(GetFirstPC())
        + "|e_area=" + Tag(e) + "|e_act=" + IntToString(GetCurrentAction(e)) + "|e_target=" + Id(GetAttackTarget(e)) + "|e_pos=" + Pos(e)
        + "|n2_area=" + Tag(n2) + "|n2_act=" + IntToString(GetCurrentAction(n2)) + "|n2_target=" + Id(GetAttackTarget(n2)) + "|n2_pos=" + Pos(n2));
    DelayCommand(0.25, Chase(n - 1));
}
void FindEnc()
{
    object e = GetNearestObjectByTag("PAI_E", GetFirstPC());
    SetLocalObject(M(), "PAI_EC", e);
    if (GetIsObjectValid(e)) Strip(e);
    Note("enc|valid=" + IntToString(GetIsObjectValid(e)) + "|is_enc=" + IntToString(GetIsEncounterCreature(e)) + "|pos=" + Pos(e)
        + "|hostile=" + IntToString(GetIsEnemy(GetFirstPC(), e)));
}
void Order()
{
    object pc = GetFirstPC();
    AssignCommand(O("PAI_EC"), ActionAttack(pc, FALSE));
    AssignCommand(O("PAI_N2"), ActionAttack(pc, FALSE));
    Note("ordered|" + FloatToString(T(), 0, 2));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc) || GetLocalInt(M(), "started")) return;
    SetLocalInt(M(), "started", 1);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(400), pc);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(0.5, VCamera(pc, 0.0, 16.0, 60.0));
    DelayCommand(1.0, Setup());
    DelayCommand(1.5, Levels(44));
    string a = GetTag(GetArea(pc));
    object oa = GetArea(pc);
    // 1. Away to area B at 8 s, back at 16 s.
    DelayCommand(8.0, Note("jump_b|" + FloatToString(T(), 0, 2)));
    DelayCommand(8.0, Jump(pc, B(0.0, 0.0)));
    DelayCommand(16.0, Note("jump_a|" + FloatToString(T(), 0, 2)));
    DelayCommand(16.0, Jump(pc, In(a, 20.0, 20.0)));
    // 2a. Alone: T1 2 m east, attacked at 25 s, jumps away at 29 s.
    DelayCommand(24.0, RemoveHenchman(pc, O("PAI_H")));
    DelayCommand(24.1, Gone("PAI_H"));
    DelayCommand(24.1, Gone("PAI_N1"));
    DelayCommand(24.2, Jump(pc, In(a, 20.0, 20.0)));
    DelayCommand(24.5, MakeV("PAI_T1", oa, 22.0, 20.0, 180.0, TRUE));
    DelayCommand(25.0, Attack(O("PAI_T1")));
    DelayCommand(28.5, Sample("atk1", 40));
    DelayCommand(29.0, Leave("atk1", O("PAI_T1")));
    // 2b. T3 attacked 2 m north, T2 1.4 m east of her; T3 jumps away at 39 s.
    DelayCommand(33.5, Jump(pc, In(a, 20.0, 20.0)));
    DelayCommand(34.0, MakeV("PAI_T3", oa, 20.0, 22.0, 270.0, TRUE));
    DelayCommand(34.0, MakeV("PAI_T2", oa, 21.4, 20.0, 180.0, TRUE));
    DelayCommand(35.0, Attack(O("PAI_T3")));
    DelayCommand(38.5, Sample("atk2", 40));
    DelayCommand(39.0, Leave("atk2", O("PAI_T3")));
    DelayCommand(43.5, Gone("PAI_T2"));
    DelayCommand(43.5, Gone("PAI_T3"));
    DelayCommand(43.5, Gone("PAI_T1"));
    DelayCommand(43.6, AssignCommand(pc, ClearAllActions(TRUE)));
    // 3. Into the encounter (spawn point 45,20), back, then both ordered at 53 s; she jumps away at 54.5 s.
    DelayCommand(44.0, AssignCommand(pc, ActionMoveToLocation(Location(oa, Vector(33.0, 20.0, 0.0), 0.0), TRUE)));
    DelayCommand(48.0, FindEnc());
    DelayCommand(48.1, MakeV("PAI_N2", oa, 45.0, 24.0, 180.0, TRUE));
    DelayCommand(48.5, Jump(pc, In(a, 20.0, 20.0)));
    DelayCommand(53.0, Order());
    DelayCommand(53.0, Chase(64));
    DelayCommand(54.5, Note("jump_b2|" + FloatToString(T(), 0, 2)));
    DelayCommand(54.5, Jump(pc, B(0.0, 0.0)));
    DelayCommand(71.0, VShot("a_end"));
    DelayCommand(73.0, VDone());
}
