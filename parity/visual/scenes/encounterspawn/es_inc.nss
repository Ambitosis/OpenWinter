#include "visual_inc"
// encounterspawn helpers: every reading is a "VISUAL|note|es|..." line.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|es|" + s); }
object Mod() { return GetModule(); }
object Enc(string sLabel) { return GetObjectByTag("ES_" + GetStringUpperCase(sLabel)); }
string F1(float f) { return FloatToString(f, 0, 1); }

string EncState(object oEnc)
{
    return "cur=" + IntToString(GetEncounterSpawnsCurrent(oEnc)) + "|active=" + IntToString(GetEncounterActive(oEnc));
}

// Spawned creatures of a test (es_spawn records them).
int Count(string sLabel) { return GetLocalInt(Mod(), "n_" + sLabel); }
object Spawned(string sLabel, int i) { return GetLocalObject(Mod(), "o_" + sLabel + "_" + IntToString(i)); }

void Report(string sLabel, string sWhen)
{
    int i, nAlive = 0, nValid = 0;
    string sTags = "";
    for (i = 1; i <= Count(sLabel); i++)
    {
        object o = Spawned(sLabel, i);
        if (!GetIsObjectValid(o)) continue;
        nValid++;
        if (!GetIsDead(o)) { nAlive++; sTags += GetTag(o) + ","; }
    }
    Log("report|" + sLabel + "|" + sWhen + "|spawned=" + IntToString(Count(sLabel)) + "|valid=" + IntToString(nValid)
        + "|alive=" + IntToString(nAlive) + "|tags=" + sTags + "|" + EncState(Enc(sLabel)) + "|pchd=" + IntToString(GetHitDice(GetFirstPC())));
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
        if (GetIsObjectValid(o)) { AssignCommand(o, SetIsDestroyable(TRUE)); DestroyObject(o); }
    }
}

// The player walks into the test's encounter from 5 m south of its centre.
void Go(string sLabel)
{
    object pc = GetFirstPC();
    object oEnc = Enc(sLabel);
    vector c = GetPosition(oEnc);
    SetLocalString(Mod(), "cur", sLabel);
    Log("go|" + sLabel + "|" + EncState(oEnc));
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(c.x, c.y - 5.0, 0.0), 90.0)));
    AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(c.x, c.y, 0.0), 90.0)));
}

// Walks back out (to 5 m south) or in again (to the centre).
void Leave(string sLabel)
{
    object pc = GetFirstPC();
    vector c = GetPosition(Enc(sLabel));
    Log("leave|" + sLabel);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(c.x, c.y - 5.0, 0.0), 90.0)));
}

void Enter(string sLabel)
{
    object pc = GetFirstPC();
    vector c = GetPosition(Enc(sLabel));
    SetLocalString(Mod(), "cur", sLabel);
    Log("reenter|" + sLabel + "|" + EncState(Enc(sLabel)));
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(c.x, c.y, 0.0), 90.0)));
}
