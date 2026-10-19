#include "er_inc"
// encounterrules: how the original sizes and repeats encounters (docs/ENCOUNTERS.md). Written by
// parity/gen_encounterrules.py (scene.json, fixtures, er_plan.nss); this script is the timeline.
// A: the player (level 1) walks into groups of six identical encounters (difficulty x list CR);
// B, C: with a level 1, then a level 10 henchman walking in beside her; hen0: the henchman alone (PlayerOnly off);
// npc0: a commoner NPC (PlayerOnly off); D, E: the player at level 5 and 10;
// so1/so0/so1n/so1t/so1r0/alive: spawn option, respawn count, reset and reset time, re-entered;
// fac2: an encounter of the commoner faction; nosp2: no spawn points, rectangle off the position; face: spawn points' facing.

void Group(string sLabel, float t)
{
    DelayCommand(t, Go(sLabel));
    DelayCommand(t + 3.5, Report(sLabel, "end"));
    DelayCommand(t + 4.0, Clean(sLabel));
}

void Hire(string sResRef, string sLabel)
{
    object pc = GetFirstPC();
    vector c = ErSpot(sLabel);
    object h = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), c + Vector(1.0, -4.0, 0.0), 90.0), FALSE, "ER_HENCH");
    SetLocalObject(Mod(), "hench", h);
    AddHenchman(pc, h);
    SetAILevel(h, AI_LEVEL_NORMAL);
    Walk(h, c + Vector(1.0, 0.0, 0.0));
    Log("hire|" + sResRef + "|hd=" + IntToString(GetHitDice(h)) + "|master=" + GetName(GetMaster(h)));
}

void HenchGo(string sLabel)
{
    object h = GetLocalObject(Mod(), "hench");
    SetLocalString(Mod(), "cur", sLabel);
    vector c = ErSpot(sLabel);
    AssignCommand(h, ClearAllActions(TRUE));
    AssignCommand(h, JumpToLocation(Location(GetArea(h), c + Vector(0.0, -4.0, 0.0), 90.0)));
    AssignCommand(h, ActionMoveToLocation(Location(GetArea(h), c, 90.0)));
    Log("henchgo|" + sLabel + "|" + EncState(Enc(sLabel)));
}

void Fire()
{
    object h = GetLocalObject(Mod(), "hench");
    RemoveHenchman(GetFirstPC(), h);
    DestroyObject(h);
    Log("fire");
}

void Npc(string sLabel)
{
    object pc = GetFirstPC();
    vector c = ErSpot(sLabel);
    SetLocalString(Mod(), "cur", sLabel);
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", Location(GetArea(pc), c + Vector(0.0, -4.0, 0.0), 90.0), FALSE, "ER_NPC");
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetAILevel(o, AI_LEVEL_NORMAL);
    Walk(o, c);
    Log("npc|" + sLabel + "|hd=" + IntToString(GetHitDice(o)) + "|enemy=" + IntToString(GetIsEnemy(o, GetLocalObject(Mod(), "ref_hostile"))));
}

void LevelTo(int nXP, int nLevels)
{
    object pc = GetFirstPC();
    SetXP(pc, nXP);
    int i;
    for (i = 0; i < nLevels; i++) LevelUpHenchman(pc, CLASS_TYPE_SORCERER, TRUE);
    Log("level|hd=" + IntToString(GetHitDice(pc)) + "|xp=" + IntToString(GetXP(pc)));
}

void Refs()
{
    object pc = GetFirstPC();
    object a = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", Location(GetArea(pc), Vector(150.0, 150.0, 0.0), 0.0), FALSE, "ER_REF_H");
    ChangeToStandardFaction(a, STANDARD_FACTION_HOSTILE);
    object b = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", Location(GetArea(pc), Vector(155.0, 150.0, 0.0), 0.0), FALSE, "ER_REF_C");
    ChangeToStandardFaction(b, STANDARD_FACTION_COMMONER);
    SetLocalObject(Mod(), "ref_hostile", a);
    SetLocalObject(Mod(), "ref_commoner", b);
}

// Spawn, kill, stay 6 s, leave, come back 8 s later, kill, leave.
void Stay(string sLabel, float t)
{
    DelayCommand(t, Go(sLabel));
    DelayCommand(t + 3.0, Report(sLabel, "a")); DelayCommand(t + 3.1, KillAll(sLabel));
    DelayCommand(t + 9.0, Report(sLabel, "b"));
    DelayCommand(t + 9.5, Leave(sLabel));
    DelayCommand(t + 17.0, Enter(sLabel));
    DelayCommand(t + 20.0, Report(sLabel, "c")); DelayCommand(t + 20.1, KillAll(sLabel));
    DelayCommand(t + 20.5, Leave(sLabel));
}

// Walk in (jump close first when bJump), report after 3 s, kill unless bAlive, walk out.
void Cycle(string sLabel, float t, int bJump, string sWhen, int bAlive)
{
    if (bJump) DelayCommand(t, Go(sLabel)); else DelayCommand(t, Enter(sLabel));
    DelayCommand(t + 3.0, Report(sLabel, sWhen));
    if (!bAlive) DelayCommand(t + 3.1, KillAll(sLabel));
    DelayCommand(t + 3.5, Leave(sLabel));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc) || GetLocalInt(Mod(), "started")) return;
    SetLocalInt(Mod(), "started", 1);
    SetPlotFlag(pc, TRUE);
    Refs();
    Log("start|pchd=" + IntToString(GetHitDice(pc)) + "|pc=" + GetName(pc));

    float t = 4.0;
    Group("n1", t); t += 4.5;
    Group("n025", t); t += 4.5;
    Group("n05", t); t += 4.5;
    Group("n01", t); t += 4.5;
    Group("h1", t); t += 4.5;
    Group("h025", t); t += 4.5;
    Group("i1", t);
    DelayCommand(t + 2.5, VCamera(pc, 70.0, 16.0, 50.0));
    DelayCommand(t + 3.2, VShot("i1"));
    t += 4.5;
    Group("i3", t); t += 4.5;
    Group("e025", t); t += 4.5;
    Group("v025", t); t += 4.5;
    Group("n2", t); t += 4.5;

    DelayCommand(t, Hire("er_h1", "b025"));
    Group("b025", t); t += 4.5;
    DelayCommand(t, Fire()); t += 0.5;
    DelayCommand(t, Hire("er_h10", "c1"));
    Group("c1", t); t += 4.5;
    DelayCommand(t, HenchGo("hen0"));
    DelayCommand(t + 5.0, Report("hen0", "end"));
    DelayCommand(t + 5.5, Clean("hen0"));
    DelayCommand(t + 6.0, Fire());
    t += 6.5;
    DelayCommand(t, Npc("npc0"));
    DelayCommand(t + 6.0, Report("npc0", "end"));
    DelayCommand(t + 6.5, Clean("npc0"));
    DelayCommand(t + 6.5, DestroyObject(GetObjectByTag("ER_NPC")));
    t += 7.0;

    DelayCommand(t, LevelTo(10000, 4)); t += 1.0;
    Group("l5n1", t); t += 4.5;
    Group("l5n025", t); t += 4.5;
    Group("l5h1", t); t += 4.5;
    Group("l5i3", t); t += 4.5;
    DelayCommand(t, LevelTo(45000, 5)); t += 1.0;
    Group("l10n1", t); t += 4.5;
    Group("l10n3", t); t += 4.5;

    Stay("so1", t); t += 21.0;
    Stay("so0", t); t += 21.0;
    // so1n (two respawns): four rounds of spawn, kill, leave, return.
    Cycle("so1n", t, TRUE, "a", FALSE); Cycle("so1n", t + 8.0, FALSE, "b", FALSE);
    Cycle("so1n", t + 16.0, FALSE, "c", FALSE); Cycle("so1n", t + 24.0, FALSE, "d", FALSE); t += 32.0;
    // so1t (reset time 30): back after 8 s, then after 46 s; meanwhile so1r0 (no reset) and alive (left alive).
    Cycle("so1t", t, TRUE, "a", FALSE); Cycle("so1t", t + 12.0, FALSE, "b", FALSE);
    Cycle("so1r0", t + 17.0, TRUE, "a", FALSE); Cycle("so1r0", t + 25.0, FALSE, "b", FALSE);
    Cycle("alive", t + 30.0, TRUE, "a", TRUE); Cycle("alive", t + 38.0, FALSE, "b", TRUE);
    Cycle("so1t", t + 50.0, TRUE, "c", FALSE);
    t += 55.0;
    Group("fac2", t); t += 4.5;
    Group("nosp2", t); t += 4.5;
    Group("face", t); t += 4.5;
    DelayCommand(t, Log("end"));
    DelayCommand(t + 1.0, VDone());
}
