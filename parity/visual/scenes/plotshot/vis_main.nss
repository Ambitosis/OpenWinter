#include "visual_inc"
// plotshot: the player shoots a dummy 8 m away
// with a short bow while two hostile, script-less bandits stand 1.5 m either side of her, facing her, made 10 s
// before (both have perceived her) and put in combat 1 s before the first shot (1 point of damage from her). One is
// flagged Plot, the other only Immortal. Does either take an attack of opportunity (it starts attacking her
// within a moment of a shot)? Two trials with the roles swapped. Run at --difficulty 3 (D&D Hardcore) and 2 (Normal).

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|ps|" + s); }
object M() { return GetModule(); }
object O(string s) { return GetLocalObject(M(), s); }
float T() { return IntToFloat(GetTimeSecond() * 1000 + GetTimeMillisecond()) / 1000.0; }
string Id(object o) { return GetIsObjectValid(o) ? GetTag(o) : "none"; }

object Make(string sTag, float x, float y, float f)
{
    object pc = GetFirstPC();
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(x, y, 0.0), f), FALSE, sTag);
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    AssignCommand(o, ClearAllActions(TRUE));
    ChangeToStandardFaction(o, STANDARD_FACTION_HOSTILE);
    SetImmortal(o, TRUE);
    SetPlotFlag(o, FALSE);
    SetLocalObject(M(), sTag, o);
    return o;
}
void MakeV(string sTag, float x, float y, float f) { Make(sTag, x, y, f); }
void Hurt(object o) { object pc = GetFirstPC(); AssignCommand(pc, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), o)); }
void Plot(object o) { SetPlotFlag(o, TRUE); Note("plot|" + GetTag(o) + "|" + IntToString(GetPlotFlag(o))); }
string G(object g) { return "|" + GetTag(g) + "=" + Id(GetAttackTarget(g)) + "/" + IntToString(GetCurrentAction(g)) + "/c" + IntToString(GetIsInCombat(g))
    + "/p" + IntToString(GetPlotFlag(g)) + "/d" + FloatToString(GetDistanceToObject(g), 0, 2); }
void Sample(string sKey, string a, string b, int n)
{
    if (n <= 0) return;
    object pc = GetFirstPC();
    Note(sKey + "|" + FloatToString(T(), 0, 2) + "|pc=" + Id(GetAttackTarget(pc)) + "/" + IntToString(GetCurrentAction(pc)) + G(O(a)) + G(O(b)));
    DelayCommand(0.1, Sample(sKey, a, b, n - 1));
}
void Shoot() { object pc = GetFirstPC(); AssignCommand(pc, ClearAllActions(TRUE)); AssignCommand(pc, ActionAttack(O("PS_D"))); Note("shoot|" + FloatToString(T(), 0, 2)); }
void Stop() { AssignCommand(GetFirstPC(), ClearAllActions(TRUE)); }
void Gone(string s) { DestroyObject(O(s)); }
void Home() { object pc = GetFirstPC(); AssignCommand(pc, ClearAllActions(TRUE)); AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(20.0, 20.0, 0.0), 0.0))); }

// One trial from t: north guard sN, south guard sS; sPlot names the one made plot.
void Trial(string sKey, string sN, string sS, string sPlot, float t)
{
    DelayCommand(t, Home());
    DelayCommand(t + 0.5, MakeV(sN, 20.0, 21.5, 270.0));
    DelayCommand(t + 0.5, MakeV(sS, 20.0, 18.5, 90.0));
    DelayCommand(t + 10.0, Hurt(O(sN)));
    DelayCommand(t + 10.0, Hurt(O(sS)));
    DelayCommand(t + 10.2, Plot(O(sPlot)));
    DelayCommand(t + 10.5, Sample(sKey, sN, sS, 90));
    DelayCommand(t + 11.0, Shoot());
    DelayCommand(t + 19.5, Stop());
    DelayCommand(t + 20.0, Gone(sN));
    DelayCommand(t + 20.0, Gone(sS));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc) || GetLocalInt(M(), "started")) return;
    SetLocalInt(M(), "started", 1);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(400), pc);
    Note("difficulty|" + IntToString(GetGameDifficulty()));
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(0.5, VCamera(pc, 270.0, 12.0, 60.0));
    object bow = CreateItemOnObject("nw_wbwsh001", pc);
    object arrows = CreateItemOnObject("nw_wamar001", pc, 99);
    SetIdentified(bow, TRUE);
    AssignCommand(pc, ActionEquipItem(bow, INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(pc, ActionEquipItem(arrows, INVENTORY_SLOT_ARROWS));
    DelayCommand(0.5, MakeV("PS_D", 28.0, 20.0, 180.0));
    Trial("t1", "PS_PLOT1", "PS_IMM1", "PS_PLOT1", 1.0);
    Trial("t2", "PS_IMM2", "PS_PLOT2", "PS_PLOT2", 22.0);
    DelayCommand(43.0, VShot("a_end"));
    DelayCommand(45.0, VDone());
}
