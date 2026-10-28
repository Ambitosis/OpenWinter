#include "visual_inc"
// The player's attack when its target dies: does she take up another enemy, and which? Each case puts her at
// (20,20) with script-less hostile bandits around her, orders ActionAttack on T, kills T and samples her action,
// attack target and position every 0.1 s for 5 s.
//   alone   T 2 m east, nothing else
//   near    T 2 m north, N 1.4 m east
//   both    T 2 m north, N 1.4 m east, F 6 m west
//   far     T 2 m north, F 6 m west
//   neutral T 2 m north, a merchant-faction bandit 1.4 m east
// Then the same for a script-less hostile NPC attacker (A, at 30,20) against merchant bandits, ordered
// ActionAttack(T, FALSE) or ActionAttack(T, TRUE):
//   npc / npcp  T 2 m north of A, N 1.4 m east of A
// She carries temporary hit points; the bandits that live are immortal.

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|pk|" + s); }
object M() { return GetModule(); }
object O(string s) { return GetLocalObject(M(), s); }
float T() { return IntToFloat(GetTimeSecond() * 1000 + GetTimeMillisecond()) / 1000.0; }
string Id(object o) { return GetIsObjectValid(o) ? GetTag(o) : "none"; }
string Pos(object o) { vector v = GetPosition(o); return FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2); }

void Make(string sTag, float x, float y, float f, int bHostile, int bImmortal)
{
    object a = GetArea(GetFirstPC());
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(a, Vector(x, y, 0.0), f), FALSE, sTag);
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    AssignCommand(o, ClearAllActions(TRUE));
    ChangeToStandardFaction(o, bHostile ? STANDARD_FACTION_HOSTILE : STANDARD_FACTION_MERCHANT);
    SetPlotFlag(o, FALSE);
    SetImmortal(o, bImmortal);
    SetLocalObject(M(), sTag, o);
}
void Sample(string sKey, int n)
{
    if (n <= 0) return;
    object pc = GetFirstPC();
    Note(sKey + "|" + FloatToString(T(), 0, 2) + "|act=" + IntToString(GetCurrentAction(pc)) + "|target=" + Id(GetAttackTarget(pc))
        + "|pos=" + Pos(pc) + "|combat=" + IntToString(GetIsInCombat(pc)));
    DelayCommand(0.1, Sample(sKey, n - 1));
}
void Attack() { object pc = GetFirstPC(); AssignCommand(pc, ClearAllActions(TRUE)); AssignCommand(pc, ActionAttack(O("PK_T"))); }
void Clear();
void Kill(string sKey);
void SampleA(string sKey, int n)
{
    if (n <= 0) return;
    object a = O("PK_A");
    Note(sKey + "|" + FloatToString(T(), 0, 2) + "|act=" + IntToString(GetCurrentAction(a)) + "|target=" + Id(GetAttackTarget(a))
        + "|pos=" + Pos(a) + "|combat=" + IntToString(GetIsInCombat(a)));
    DelayCommand(0.1, SampleA(sKey, n - 1));
}
void AttackA(int bPassive) { AssignCommand(O("PK_A"), ActionAttack(O("PK_T"), bPassive)); }
void CaseA(float t, string sKey, int bPassive)
{
    DelayCommand(t, Clear());
    DelayCommand(t + 0.5, Make("PK_A", 30.0, 20.0, 90.0, TRUE, TRUE));
    DelayCommand(t + 0.5, Make("PK_T", 30.0, 22.0, 270.0, FALSE, FALSE));
    DelayCommand(t + 0.5, Make("PK_N", 31.4, 20.0, 180.0, FALSE, TRUE));
    DelayCommand(t + 1.0, AttackA(bPassive));
    DelayCommand(t + 3.5, SampleA(sKey, 50));
    DelayCommand(t + 4.0, Kill(sKey));
}
void Kill(string sKey) { Note(sKey + "|kill|" + FloatToString(T(), 0, 2)); ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), O("PK_T")); }
void Clear()
{
    object pc = GetFirstPC();
    DestroyObject(O("PK_T")); DestroyObject(O("PK_N")); DestroyObject(O("PK_F")); DestroyObject(O("PK_A"));
    DeleteLocalObject(M(), "PK_T"); DeleteLocalObject(M(), "PK_N"); DeleteLocalObject(M(), "PK_F"); DeleteLocalObject(M(), "PK_A");
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(20.0, 20.0, 0.0), 0.0)));
}
// One case starting at t: set up at t, attack at t+1, kill at t+4, sample t+3.5 to t+8.5, clear at t+9.
void Case(float t, string sKey, int bNorth, int bNear, int bFar, int bNeutral)
{
    DelayCommand(t, Clear());
    if (bNorth) DelayCommand(t + 0.5, Make("PK_T", 20.0, 22.0, 270.0, TRUE, FALSE));
    else DelayCommand(t + 0.5, Make("PK_T", 22.0, 20.0, 180.0, TRUE, FALSE));
    if (bNear || bNeutral) DelayCommand(t + 0.5, Make("PK_N", 21.4, 20.0, 180.0, !bNeutral, TRUE));
    if (bFar) DelayCommand(t + 0.5, Make("PK_F", 14.0, 20.0, 0.0, TRUE, TRUE));
    DelayCommand(t + 1.0, Attack());
    DelayCommand(t + 3.5, Sample(sKey, 50));
    DelayCommand(t + 4.0, Kill(sKey));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc) || GetLocalInt(M(), "started")) return;
    SetLocalInt(M(), "started", 1);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(400), pc);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(0.5, VCamera(pc, 0.0, 16.0, 60.0));
    Case(2.0, "alone", FALSE, FALSE, FALSE, FALSE);
    Case(12.0, "near", TRUE, TRUE, FALSE, FALSE);
    Case(22.0, "both", TRUE, TRUE, TRUE, FALSE);
    Case(32.0, "far", TRUE, FALSE, TRUE, FALSE);
    Case(42.0, "neutral", TRUE, FALSE, FALSE, TRUE);
    CaseA(52.0, "npc", FALSE);
    CaseA(62.0, "npcp", TRUE);
    DelayCommand(72.0, Clear());
    DelayCommand(73.0, VDone());
}
