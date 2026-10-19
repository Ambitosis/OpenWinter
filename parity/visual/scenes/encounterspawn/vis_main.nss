#include "es_inc"
// encounterspawn: how the original spawns encounters (docs/ENCOUNTERS.md). The player (level 1, plot) walks into
// one encounter after another (scene.json: 29 labelled encounters on a 16 m grid, goblin copies with 1 hit point
// and only an OnSpawn script) and every spawn, encounter event and count is logged:
// - d0..d4: one CR 1 entry at each difficulty; c025..c5, c5r3, m4: one entry of other list CRs, min and max;
//   mix: three CRs; uniq, uniq2: unique entries; nosp, face: no spawn points, two spawn points with facings;
// - npc0, npc1: a commoner NPC walks in (PlayerOnly off / on) while the player is elsewhere;
// - cont: continuous (SpawnOption 0, Respawns 3): each wave is killed while the player stays inside;
// - ra..re: reset (ResetTime 6 / 1, creatures killed or left alive, Reset off), re-entered later;
// - l5, l5d4, p1, p2: the player at level 5, then with a level 10 henchman beside her and far away.

void Count1(string sLabel, float t)
{
    DelayCommand(t, Go(sLabel));
    DelayCommand(t + 4.5, Report(sLabel, "end"));
    DelayCommand(t + 5.0, Clean(sLabel));
}

void Npc(string sLabel)
{
    object pc = GetFirstPC();
    vector c = GetPosition(Enc(sLabel));
    SetLocalString(Mod(), "cur", sLabel);
    Log("npc|" + sLabel + "|" + EncState(Enc(sLabel)));
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", Location(GetArea(pc), Vector(c.x, c.y - 5.0, 0.0), 90.0), FALSE, "ES_NPC_" + sLabel);
    SetLocalObject(Mod(), "npc_" + sLabel, o);
    SetAILevel(o, AI_LEVEL_NORMAL);
    AssignCommand(o, ActionMoveToLocation(Location(GetArea(pc), Vector(c.x, c.y, 0.0), 90.0)));
}

void NpcEnd(string sLabel)
{
    Report(sLabel, "end");
    Clean(sLabel);
    DestroyObject(GetLocalObject(Mod(), "npc_" + sLabel));
}

void LevelUp()
{
    object pc = GetFirstPC();
    SetXP(pc, 10000);
    int i;
    for (i = 0; i < 4; i++) LevelUpHenchman(pc, CLASS_TYPE_SORCERER, TRUE);
    Log("level|hd=" + IntToString(GetHitDice(pc)) + "|xp=" + IntToString(GetXP(pc)));
}

void Hire(string sLabel)
{
    object pc = GetFirstPC();
    vector c = GetPosition(Enc(sLabel));
    object h = CreateObject(OBJECT_TYPE_CREATURE, "es_h10", Location(GetArea(pc), Vector(c.x + 1.0, c.y - 5.0, 0.0), 90.0), FALSE, "ES_HENCH");
    SetLocalObject(Mod(), "hench", h);
    AddHenchman(pc, h);
    SetAILevel(h, AI_LEVEL_NORMAL);
    AssignCommand(h, ActionMoveToLocation(Location(GetArea(pc), Vector(c.x + 1.0, c.y, 0.0), 90.0)));
    Log("hire|hd=" + IntToString(GetHitDice(h)) + "|master=" + GetName(GetMaster(h)) + "|pchd=" + IntToString(GetHitDice(pc)));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc) || GetLocalInt(Mod(), "started")) return;
    SetLocalInt(Mod(), "started", 1);
    SetPlotFlag(pc, TRUE);
    Log("start|pchd=" + IntToString(GetHitDice(pc)) + "|pc=" + GetName(pc));

    float t = 4.0;
    Count1("d0", t); t += 6.0;
    Count1("d1", t); t += 6.0;
    Count1("d2", t); t += 6.0;
    Count1("d3", t); t += 6.0;
    Count1("d4", t); t += 6.0;
    Count1("c025", t);
    DelayCommand(t + 3.0, VCamera(pc, 70.0, 14.0, 50.0));
    DelayCommand(t + 4.0, VShot("c025"));
    t += 6.0;
    Count1("c05", t); t += 6.0;
    Count1("c2", t); t += 6.0;
    Count1("c3", t); t += 6.0;
    Count1("c5", t); t += 6.0;
    Count1("c5r3", t); t += 6.0;
    Count1("m4", t); t += 6.0;
    Count1("mix", t); t += 6.0;
    Count1("uniq", t); t += 6.0;
    Count1("uniq2", t); t += 6.0;
    Count1("nosp", t); t += 6.0;
    Count1("face", t); t += 6.0;

    DelayCommand(t, Npc("npc0"));
    DelayCommand(t + 6.0, NpcEnd("npc0"));
    t += 7.0;
    DelayCommand(t, Npc("npc1"));
    DelayCommand(t + 6.0, NpcEnd("npc1"));
    t += 7.0;

    // Continuous: the player stays inside; each wave is killed.
    DelayCommand(t, Go("cont"));
    DelayCommand(t + 4.0, Report("cont", "w1")); DelayCommand(t + 4.1, KillAll("cont"));
    DelayCommand(t + 10.0, Report("cont", "w2")); DelayCommand(t + 10.1, KillAll("cont"));
    DelayCommand(t + 16.0, Report("cont", "w3")); DelayCommand(t + 16.1, KillAll("cont"));
    DelayCommand(t + 22.0, Report("cont", "w4")); DelayCommand(t + 22.1, KillAll("cont"));
    DelayCommand(t + 28.0, Report("cont", "w5"));
    DelayCommand(t + 28.5, Leave("cont"));
    DelayCommand(t + 29.0, Clean("cont"));
    t += 30.0;

    // Reset: ra re-entered 3 s after leaving.
    DelayCommand(t, Go("ra"));
    DelayCommand(t + 3.5, Report("ra", "a")); DelayCommand(t + 3.6, KillAll("ra"));
    DelayCommand(t + 4.0, Leave("ra"));
    DelayCommand(t + 7.0, Enter("ra"));
    DelayCommand(t + 10.0, Report("ra", "b"));
    DelayCommand(t + 10.5, Leave("ra"));
    t += 11.0;
    // rb, rc, rd (left alive), re (no reset): in, out, and back about 20 s later.
    float t0 = t;
    DelayCommand(t, Go("rb")); DelayCommand(t + 3.5, Report("rb", "a")); DelayCommand(t + 3.6, KillAll("rb")); DelayCommand(t + 4.0, Leave("rb")); t += 5.0;
    DelayCommand(t, Go("rc")); DelayCommand(t + 3.5, Report("rc", "a")); DelayCommand(t + 3.6, KillAll("rc")); DelayCommand(t + 4.0, Leave("rc")); t += 5.0;
    DelayCommand(t, Go("rd")); DelayCommand(t + 3.5, Report("rd", "a")); DelayCommand(t + 4.0, Leave("rd")); t += 5.0;
    DelayCommand(t, Go("re")); DelayCommand(t + 3.5, Report("re", "a")); DelayCommand(t + 3.6, KillAll("re")); DelayCommand(t + 4.0, Leave("re")); t += 6.0;
    DelayCommand(t, Enter("rb")); DelayCommand(t + 3.5, Report("rb", "b")); DelayCommand(t + 4.0, Leave("rb")); t += 6.0;
    DelayCommand(t, Enter("rc")); DelayCommand(t + 3.5, Report("rc", "b")); DelayCommand(t + 4.0, Leave("rc")); t += 6.0;
    DelayCommand(t, Enter("rd")); DelayCommand(t + 3.5, Report("rd", "b")); DelayCommand(t + 4.0, Leave("rd")); t += 6.0;
    DelayCommand(t, Enter("re")); DelayCommand(t + 3.5, Report("re", "b")); DelayCommand(t + 4.0, Leave("re")); t += 6.0;

    // Level and party.
    DelayCommand(t, LevelUp()); t += 1.0;
    Count1("l5", t); t += 6.0;
    Count1("l5d4", t); t += 6.0;
    DelayCommand(t, Hire("p1"));
    Count1("p1", t); t += 6.0;
    Count1("p2", t); t += 6.0;
    DelayCommand(t, Log("end"));
    DelayCommand(t + 1.0, VDone());
}
