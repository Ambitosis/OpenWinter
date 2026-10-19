#include "visual_inc"
// encounterwaves helpers (the timeline is vis_main.nss, written by parity/gen_encounterwaves.py). Every reading is a
// "VISUAL|note|ew|..." line. Spawned creatures are filed under the running trial's label by ew_spawn.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|ew|" + s); }
object Mod() { return GetModule(); }

// Counts what spawned since the last count, kills it (nMode 1) or destroys it (nMode 2), and logs the count.
void Count(string sLabel, string sWhen, int nMode)
{
    object e = GetObjectByTag("EW_" + GetStringUpperCase(sLabel));
    int n = GetLocalInt(Mod(), "n_" + sLabel), i, alive = 0;
    float cr = -1.0;
    for (i = 1; i <= n; i++)
    {
        object o = GetLocalObject(Mod(), "o_" + sLabel + "_" + IntToString(i));
        if (!GetIsObjectValid(o)) continue;
        if (!GetIsDead(o)) alive++;
        cr = GetChallengeRating(o);
        if (nMode == 1 && !GetIsDead(o)) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
        else if (nMode == 2) DestroyObject(o);
    }
    Log("count|" + sLabel + "|" + sWhen + "|n=" + IntToString(n) + "|alive=" + IntToString(alive) + "|cr=" + FloatToString(cr, 0, 2)
        + "|cur=" + IntToString(GetEncounterSpawnsCurrent(e)) + "|active=" + IntToString(GetEncounterActive(e)));
    SetLocalInt(Mod(), "n_" + sLabel, 0);
}

void Walk(object o, vector vFrom, vector vTo, int bRun = FALSE)
{
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, JumpToLocation(Location(GetArea(o), vFrom, 90.0)));
    AssignCommand(o, ActionMoveToLocation(Location(GetArea(o), vTo, 90.0), bRun));
}

void Quiet(object o)
{
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
}

// A level 12 creature standing still at a point: a henchman ("h12") or a plain NPC ("n12").
void Helper(string sWho, vector v)
{
    object pc = GetFirstPC();
    object h = CreateObject(OBJECT_TYPE_CREATURE, "ew_h12", Location(GetArea(pc), v, 90.0), FALSE, "EW_HELPER");
    Quiet(h);
    if (sWho == "h12")
    {
        SetMaxHenchmen(5);
        AddHenchman(pc, h);
    }
    else ChangeToStandardFaction(h, STANDARD_FACTION_COMMONER);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), h);
    Log("helper|" + sWho + "|hd=" + IntToString(GetHitDice(h)) + "|master=" + GetName(GetMaster(h)) + "|at=" + FloatToString(v.x, 0, 1) + "," + FloatToString(v.y, 0, 1));
}

void HelperGone()
{
    object pc = GetFirstPC();
    object h = GetObjectByTag("EW_HELPER");
    while (GetIsObjectValid(h))
    {
        if (GetMaster(h) == pc) RemoveHenchman(pc, h);
        DestroyObject(h);
        SetTag(h, "EW_GONE");
        h = GetObjectByTag("EW_HELPER");
    }
}

void Cur(string sLabel) { SetLocalString(Mod(), "cur", sLabel); }

// A range trial: the helper at (dx, dy) from the point she enters the 6 m square (its south edge), then she walks in.
void Range(string sLabel, float x, float y, string sWho, float dx, float dy, float t)
{
    DelayCommand(t, Cur(sLabel));
    if (sWho != "") DelayCommand(t, Helper(sWho, Vector(x + dx, y - 3.0 + dy, 0.0)));
    DelayCommand(t + 0.5, Walk(GetFirstPC(), Vector(x, y - 7.0, 0.0), Vector(x, y, 0.0)));
    DelayCommand(t + 6.0, Count(sLabel, "wave", 2));
    DelayCommand(t + 6.5, HelperGone());
}

void Off(string sLabel) { SetEncounterActive(FALSE, GetObjectByTag("EW_" + GetStringUpperCase(sLabel))); }

// A plain creature walks through the square from the west and is destroyed once out.
void Passer(vector v)
{
    object pc = GetFirstPC();
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), v + Vector(-9.0, 1.0, 0.0), 0.0), FALSE, "EW_PASSER");
    Quiet(o);
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetPlotFlag(o, TRUE);
    AssignCommand(o, ActionMoveToLocation(Location(GetArea(pc), v + Vector(9.0, 1.0, 0.0), 0.0), TRUE));
}

void PasserGone()
{
    object o = GetObjectByTag("EW_PASSER");
    vector v = GetPosition(o);
    Log("passer|gone|at=" + FloatToString(v.x, 0, 1) + "," + FloatToString(v.y, 0, 1));
    DestroyObject(o);
}

// Wave trial: she walks in and stays; each wave is counted and killed; the encounter respawns 4 s after it is
// exhausted. bPasser: a creature walks through after her and is destroyed before the respawn; she then walks out
// and back in.
void Waves(string sLabel, float x, float y, int bPasser, float t)
{
    object pc = GetFirstPC();
    vector v = Vector(x, y, 0.0);
    DelayCommand(t, Cur(sLabel));
    DelayCommand(t, Walk(pc, v + Vector(0.0, -6.0, 0.0), v));
    DelayCommand(t + 4.0, Count(sLabel, "a", 1));
    if (bPasser)
    {
        DelayCommand(t + 4.2, Passer(v));
        DelayCommand(t + 8.0, PasserGone());
        DelayCommand(t + 13.0, Count(sLabel, "b", 1));
        DelayCommand(t + 14.0, Walk(pc, v, v + Vector(0.0, -8.0, 0.0)));
        DelayCommand(t + 19.0, Walk(pc, v + Vector(0.0, -8.0, 0.0), v));
        DelayCommand(t + 24.0, Count(sLabel, "c", 1));
        DelayCommand(t + 32.0, Count(sLabel, "d", 1));
    }
    else
    {
        DelayCommand(t + 12.0, Count(sLabel, "b", 1));
        DelayCommand(t + 20.0, Count(sLabel, "c", 1));
        DelayCommand(t + 28.0, Count(sLabel, "d", 1));
    }
    DelayCommand(t + 36.0, Off(sLabel));
    DelayCommand(t + 36.5, Count(sLabel, "e", 2));
}
