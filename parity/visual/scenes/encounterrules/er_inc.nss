#include "visual_inc"
#include "er_plan"
// encounterrules helpers: every reading is a "VISUAL|note|er|..." line. A test's encounters are tagged ER_<LABEL>
// (singles) or ER_<LABEL>_1..6 (groups); er_spawn files every spawned creature under the running test.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|er|" + s); }
object Mod() { return GetModule(); }
string F1(float f) { return FloatToString(f, 0, 1); }
object Enc(string sLabel)
{
    object o = GetObjectByTag("ER_" + GetStringUpperCase(sLabel));
    return GetIsObjectValid(o) ? o : GetObjectByTag("ER_" + GetStringUpperCase(sLabel) + "_1");
}

string EncState(object oEnc)
{
    return "cur=" + IntToString(GetEncounterSpawnsCurrent(oEnc)) + "|max=" + IntToString(GetEncounterSpawnsMax(oEnc))
        + "|active=" + IntToString(GetEncounterActive(oEnc));
}

int Count(string sLabel) { return GetLocalInt(Mod(), "n_" + sLabel); }
object Spawned(string sLabel, int i) { return GetLocalObject(Mod(), "o_" + sLabel + "_" + IntToString(i)); }

void Report(string sLabel, string sWhen)
{
    int i, nAlive = 0;
    int n1 = 0, n2 = 0, n3 = 0, n4 = 0, n5 = 0, n6 = 0;
    for (i = 1; i <= Count(sLabel); i++)
    {
        object o = Spawned(sLabel, i);
        if (!GetIsObjectValid(o) || GetIsDead(o)) continue;
        nAlive++;
        string t = GetTag(o);
        if (t == "ES_1") n1++; else if (t == "ES_2") n2++; else if (t == "ES_3") n3++;
        else if (t == "ES_4") n4++; else if (t == "ES_5") n5++; else if (t == "ES_6") n6++;
    }
    Log("report|" + sLabel + "|" + sWhen + "|spawned=" + IntToString(Count(sLabel)) + "|alive=" + IntToString(nAlive)
        + "|by=" + IntToString(n1) + "," + IntToString(n2) + "," + IntToString(n3) + "," + IntToString(n4) + "," + IntToString(n5) + "," + IntToString(n6)
        + "|" + EncState(Enc(sLabel)) + "|pchd=" + IntToString(GetHitDice(GetFirstPC())));
}

void KillAll(string sLabel)
{
    int i;
    for (i = 1; i <= Count(sLabel); i++)
    {
        object o = Spawned(sLabel, i);
        if (GetIsObjectValid(o) && !GetIsDead(o)) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
    }
}

void Clean(string sLabel)
{
    int i;
    for (i = 1; i <= Count(sLabel); i++)
    {
        object o = Spawned(sLabel, i);
        if (GetIsObjectValid(o)) DestroyObject(o);
    }
}

void Walk(object o, vector v)
{
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, ActionMoveToLocation(Location(GetArea(o), v, 90.0)));
}

// The player walks into the test's encounters from 4 m south of the spot.
void Go(string sLabel)
{
    object pc = GetFirstPC();
    vector c = ErSpot(sLabel);
    SetLocalString(Mod(), "cur", sLabel);
    Log("go|" + sLabel + "|" + EncState(Enc(sLabel)));
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), c + Vector(0.0, -4.0, 0.0), 90.0)));
    AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), c, 90.0)));
}

void Leave(string sLabel)
{
    Log("leave|" + sLabel);
    Walk(GetFirstPC(), ErSpot(sLabel) + Vector(0.0, -5.0, 0.0));
}

void Enter(string sLabel)
{
    SetLocalString(Mod(), "cur", sLabel);
    Log("reenter|" + sLabel + "|" + EncState(Enc(sLabel)));
    Walk(GetFirstPC(), ErSpot(sLabel));
}
