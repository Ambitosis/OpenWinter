#include "visual_inc"
// encounterbudget helpers (the timeline is vis_main.nss, written by parity/gen_encounterbudget.py). Every reading
// is a "VISUAL|note|eb|..." line. A walk-in's six encounters are tagged EB_<LABEL>_1..6 and spawn EB_1..EB_6.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|eb|" + s); }
object Mod() { return GetModule(); }

int N(string sLabel, int k) { return GetLocalInt(Mod(), "c_" + sLabel + "_" + IntToString(k)); }

void Report(string sLabel, string sWhen, string sCrs, int nD)
{
    object pc = GetFirstPC();
    object e = GetObjectByTag("EB_" + GetStringUpperCase(sLabel) + "_1");
    Log("report|" + sLabel + "|" + sWhen + "|d=" + IntToString(nD) + "|crs=" + sCrs + "|n=" + IntToString(N(sLabel, 1)) + "," + IntToString(N(sLabel, 2))
        + "," + IntToString(N(sLabel, 3)) + "," + IntToString(N(sLabel, 4)) + "," + IntToString(N(sLabel, 5)) + "," + IntToString(N(sLabel, 6))
        + "|alive=" + IntToString(GetLocalInt(Mod(), "alive_" + sLabel)) + "|pchd=" + IntToString(GetHitDice(pc)) + "|xp=" + IntToString(GetXP(pc))
        + "|cur=" + IntToString(GetEncounterSpawnsCurrent(e)) + "|active=" + IntToString(GetEncounterActive(e)));
}

void Each(string sLabel, int nMode)
{
    int i, nAlive = 0;
    for (i = 1; i <= GetLocalInt(Mod(), "n_" + sLabel); i++)
    {
        object o = GetLocalObject(Mod(), "o_" + sLabel + "_" + IntToString(i));
        if (!GetIsObjectValid(o)) continue;
        if (nMode == 1 && !GetIsDead(o)) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
        else if (nMode == 2) DestroyObject(o);
        else if (nMode == 0 && !GetIsDead(o)) nAlive++;
    }
    if (nMode == 0) SetLocalInt(Mod(), "alive_" + sLabel, nAlive);
}

void Snapshot(string sLabel, string sWhen, string sCrs, int nD) { Each(sLabel, 0); Report(sLabel, sWhen, sCrs, nD); }

void Move(object o, vector v, int bJump)
{
    AssignCommand(o, ClearAllActions(TRUE));
    if (bJump) AssignCommand(o, JumpToLocation(Location(GetArea(o), v + Vector(0.0, -4.0, 0.0), 90.0)));
    AssignCommand(o, ActionMoveToLocation(Location(GetArea(o), v, 90.0)));
}

// The player (and her henchmen beside her) walk into the spot's encounters from 4 m south.
void Go(string sLabel, vector v)
{
    object pc = GetFirstPC();
    SetLocalString(Mod(), "cur", sLabel);
    Move(pc, v, TRUE);
    int n;
    for (n = 1; n <= 3; n++)
    {
        object h = GetHenchman(pc, n);
        if (GetIsObjectValid(h) && !GetLocalInt(h, "far")) Move(h, v + Vector(IntToFloat(n), 0.0, 0.0), TRUE);
    }
}

void Walk(string sLabel, float x, float y, int nD, string sCrs, float t)
{
    DelayCommand(t, Go(sLabel, Vector(x, y, 0.0)));
    DelayCommand(t + 3.5, Snapshot(sLabel, "end", sCrs, nD));
    DelayCommand(t + 4.0, Each(sLabel, 2));
}

// Continuous: spawn, kill, and watch the respawns with the player inside.
void Cont(string sLabel, float x, float y, float t)
{
    vector v = Vector(x, y, 0.0);
    DelayCommand(t, Go(sLabel, v));
    DelayCommand(t + 3.0, Snapshot(sLabel, "a", "", 0)); DelayCommand(t + 3.1, Each(sLabel, 1));
    DelayCommand(t + 6.0, Snapshot(sLabel, "b", "", 0));
    DelayCommand(t + 9.0, Snapshot(sLabel, "c", "", 0)); DelayCommand(t + 9.1, Each(sLabel, 1));
    DelayCommand(t + 14.0, Snapshot(sLabel, "d", "", 0));
    DelayCommand(t + 17.0, Snapshot(sLabel, "e", "", 0)); DelayCommand(t + 17.1, Each(sLabel, 1));
    DelayCommand(t + 25.0, Snapshot(sLabel, "f", "", 0));
    DelayCommand(t + 26.0, Each(sLabel, 2));
}

// Continuous, but the player walks out straight after the kill and back in 6 s later.
void ContOut(string sLabel, float x, float y, float t)
{
    vector v = Vector(x, y, 0.0);
    DelayCommand(t, Go(sLabel, v));
    DelayCommand(t + 3.0, Snapshot(sLabel, "a", "", 0)); DelayCommand(t + 3.1, Each(sLabel, 1));
    DelayCommand(t + 3.2, Move(GetFirstPC(), v + Vector(0.0, -6.0, 0.0), FALSE));
    DelayCommand(t + 9.0, Snapshot(sLabel, "b", "", 0));
    DelayCommand(t + 10.0, Move(GetFirstPC(), v, FALSE));
    DelayCommand(t + 15.0, Snapshot(sLabel, "c", "", 0));
    DelayCommand(t + 16.0, Each(sLabel, 2));
}

void LevelTo(int nXP, int nLevel)
{
    object pc = GetFirstPC();
    SetXP(pc, nXP);
    int i;
    for (i = 0; i < 20 && GetHitDice(pc) < nLevel; i++) LevelUpHenchman(pc, CLASS_TYPE_SORCERER, TRUE);
    Log("level|hd=" + IntToString(GetHitDice(pc)) + "|xp=" + IntToString(GetXP(pc)));
}

void SetXPTo(int nXP)
{
    SetXP(GetFirstPC(), nXP);
    Log("xp|hd=" + IntToString(GetHitDice(GetFirstPC())) + "|xp=" + IntToString(GetXP(GetFirstPC())));
}

void Hire(string sResRef, int n)
{
    object pc = GetFirstPC();
    object h = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), GetPosition(pc) + Vector(IntToFloat(n + 1), 0.0, 0.0), 0.0), FALSE, "EB_HENCH");
    SetMaxHenchmen(5);
    AddHenchman(pc, h);
    SetAILevel(h, AI_LEVEL_NORMAL);
    Log("hire|" + sResRef + "|hd=" + IntToString(GetHitDice(h)) + "|master=" + GetName(GetMaster(h)));
}

// A henchman left 60 m away in the same area (Go doesn't bring it).
void HireFar(string sResRef, int n)
{
    object pc = GetFirstPC();
    object h = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(150.0 - IntToFloat(n), 150.0, 0.0), 0.0), FALSE, "EB_HENCH");
    SetLocalInt(h, "far", 1);
    SetMaxHenchmen(5);
    AddHenchman(pc, h);
    Log("hirefar|" + sResRef + "|hd=" + IntToString(GetHitDice(h)) + "|master=" + GetName(GetMaster(h)));
}

void FireAll()
{
    object pc = GetFirstPC();
    object h = GetHenchman(pc, 1);
    while (GetIsObjectValid(h))
    {
        RemoveHenchman(pc, h);
        DestroyObject(h);
        h = GetHenchman(pc, 1);
    }
    Log("fire");
}
