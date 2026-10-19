#include "visual_inc"
#include "mx_plan"
// encountermix (docs/ENCOUNTERS.md): which creatures a mixed list spawns, which spawn point is used, where an
// encounter without spawn points puts them, and what happens to encounter creatures when the player leaves the
// area. The player is level 1 and every encounter is Impossible (target 6). Creature copies MX_1..MX_6 are
// the list entries in order (scene.json); mx_spawn logs each spawn.
// ma/ma2 CR 1,2,3; mb 3,2,1; mc 1,1; md 6,0.25; me 8,10; mf 1u,2,4u; mg 4u,4u,4u; mh 0.5,1,2,3,4,5; mi 1,3;
// mj 2u,2u; mk 5,3 (u = unique). sa: spawn points 8 m north and 7 m south; sb: 8 m east, 6 m west, 9 m north;
// no: no spawn points, rectangle 6 m off the position; nobig: no spawn points, 14 m square.
// dz (single shot, left alive) and dk (continuous, reset 3 s, killed) when the player goes to the other area.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|mx|" + s); }

void Report(string sLabel)
{
    object m = GetModule();
    int i, n1 = 0, n2 = 0, n3 = 0, n4 = 0, n5 = 0, n6 = 0, nValid = 0, nAlive = 0;
    for (i = 1; i <= GetLocalInt(m, "n_" + sLabel); i++)
    {
        object o = GetLocalObject(m, "o_" + sLabel + "_" + IntToString(i));
        if (!GetIsObjectValid(o)) continue;
        nValid++;
        if (GetIsDead(o)) continue;
        nAlive++;
        string t = GetTag(o);
        if (t == "MX_1") n1++; else if (t == "MX_2") n2++; else if (t == "MX_3") n3++;
        else if (t == "MX_4") n4++; else if (t == "MX_5") n5++; else if (t == "MX_6") n6++;
    }
    object e = GetObjectByTag("MX_" + GetStringUpperCase(sLabel));
    Log("report|" + sLabel + "|spawned=" + IntToString(GetLocalInt(m, "n_" + sLabel)) + "|valid=" + IntToString(nValid) + "|alive=" + IntToString(nAlive)
        + "|by=" + IntToString(n1) + "," + IntToString(n2) + "," + IntToString(n3) + "," + IntToString(n4) + "," + IntToString(n5) + "," + IntToString(n6)
        + "|cur=" + IntToString(GetEncounterSpawnsCurrent(e)) + "|active=" + IntToString(GetEncounterActive(e)));
}

void Each(string sLabel, int bKill)
{
    object m = GetModule();
    int i;
    for (i = 1; i <= GetLocalInt(m, "n_" + sLabel); i++)
    {
        object o = GetLocalObject(m, "o_" + sLabel + "_" + IntToString(i));
        if (!GetIsObjectValid(o)) continue;
        if (bKill) { if (!GetIsDead(o)) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o); }
        else DestroyObject(o);
    }
}

void Go(string sLabel)
{
    object pc = GetFirstPC();
    vector c = MxSpot(sLabel);
    SetLocalString(GetModule(), "cur", sLabel);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), c + Vector(0.0, -4.5, 0.0), 90.0)));
    AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), c, 90.0)));
}

void Walk(object o, vector v)
{
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, ActionMoveToLocation(Location(GetArea(o), v, 90.0)));
}

void Test(string sLabel, float t)
{
    DelayCommand(t, Go(sLabel));
    DelayCommand(t + 3.5, Report(sLabel));
    DelayCommand(t + 4.0, Each(sLabel, FALSE));
}

object OtherArea()
{
    object a = GetFirstArea();
    while (GetIsObjectValid(a) && GetResRef(a) != "m0q1b") a = GetNextArea();
    return a;
}

void Away()
{
    object pc = GetFirstPC();
    Log("away|" + GetTag(OtherArea()));
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(OtherArea(), Vector(45.0, 11.0, 0.0), 0.0)));
}

void Back()
{
    object pc = GetFirstPC();
    Log("back");
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetObjectByTag("VISUAL_AREA"), MxSpot("dz") + Vector(0.0, -10.0, 0.0), 90.0)));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    SetPlotFlag(pc, TRUE);
    Log("start|pchd=" + IntToString(GetHitDice(pc)));
    float t = 4.0;
    Test("ma", t); t += 4.5;
    Test("ma2", t); t += 4.5;
    Test("mb", t); t += 4.5;
    Test("mc", t); t += 4.5;
    Test("md", t); t += 4.5;
    Test("me", t); t += 4.5;
    Test("mf", t); t += 4.5;
    Test("mg", t); t += 4.5;
    Test("mh", t); t += 4.5;
    Test("mi", t); t += 4.5;
    Test("mj", t); t += 4.5;
    Test("mk", t); t += 4.5;
    Test("sa", t); t += 4.5;
    Test("sb", t); t += 4.5;
    Test("no", t); t += 4.5;
    Test("nobig", t); t += 4.5;
    // Leaving the area: dz left alive, dk's wave killed, then the player is away 30 s.
    DelayCommand(t, Go("dz"));
    DelayCommand(t + 3.5, Report("dz"));
    DelayCommand(t + 4.0, Go("dk"));
    DelayCommand(t + 7.5, Report("dk"));
    DelayCommand(t + 7.6, Each("dk", TRUE));
    DelayCommand(t + 8.0, Away());
    DelayCommand(t + 20.0, Report("dz"));
    DelayCommand(t + 20.1, Report("dk"));
    DelayCommand(t + 38.0, Back());
    DelayCommand(t + 42.0, Report("dz"));
    DelayCommand(t + 42.1, Report("dk"));
    DelayCommand(t + 43.0, Go("dz"));
    DelayCommand(t + 47.0, Report("dz"));
    DelayCommand(t + 48.0, Go("dk"));
    DelayCommand(t + 52.0, Report("dk"));
    DelayCommand(t + 54.0, VDone());
}
