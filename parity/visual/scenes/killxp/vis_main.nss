#include "visual_inc"
// killxp: how the original awards experience when a creature dies. Goblin copies with 1 hit point and set
// challenge ratings (kx0..kx25, kx.utc.json) are killed one per second, and the experience every party member
// gained is logged: by challenge rating at player levels 1 to 40 (LevelUpHenchman on the player), by module XP
// scale, with henchmen (levels 1 and 10, near and far), a familiar and a summon in the party, kills by each of them,
// by a neutral bystander, by the module and with EffectDeath, and victims of other factions or in the party.
// Every kill also sends "KX <label>" to the player first, so chat.txt shows which lines each kill printed.

// kx<i>: challenge ratings 0 .1 .125 .2 .25 .3 .33 .4 .5 .75 1 1.5 1.9 2 3 5 9 10 15 20 21 25 30 40 41 60
const int CR_COUNT = 26;

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|kx|" + s); }

object Killer(string sKey)
{
    object pc = GetFirstPC();
    if (sKey == "pc") return pc;
    if (sKey == "fam") return GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc);
    if (sKey == "sum") return GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc);
    if (sKey == "mod") return GetModule();
    return GetLocalObject(GetModule(), sKey);
}

string Classes(object o)
{
    string s = "";
    int i;
    for (i = 1; i <= 3; i++)
        if (GetClassByPosition(i, o) != CLASS_TYPE_INVALID)
            s += IntToString(GetClassByPosition(i, o)) + "x" + IntToString(GetLevelByPosition(i, o)) + " ";
    return s;
}

void Snap(string sKey)
{
    object o = Killer(sKey);
    SetLocalInt(GetModule(), "xp_" + sKey, GetIsObjectValid(o) ? GetXP(o) : -1);
}

string Delta(string sKey)
{
    object o = Killer(sKey);
    int before = GetLocalInt(GetModule(), "xp_" + sKey);
    if (!GetIsObjectValid(o) || before < 0) return "";
    return "|" + sKey + "=" + IntToString(GetXP(o) - before);
}

void DoKill(object v, int nMode)
{
    if (nMode == 1) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), v);
    else ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(20, DAMAGE_TYPE_MAGICAL), v);
}

void Report(object v, string sLabel, int bDestroy)
{
    object pc = GetFirstPC();
    Log(sLabel + "|cr=" + FloatToString(GetLocalFloat(GetModule(), "cr"), 0, 3) + "|hd=" + IntToString(GetHitDice(pc))
        + "|scale=" + IntToString(GetModuleXPScale()) + "|dead=" + IntToString(GetIsDead(v))
        + Delta("pc") + Delta("h1") + Delta("h10") + Delta("fam") + Delta("sum"));
    if (bDestroy) DestroyObject(v);
}

// One kill: the victim (a kx copy, or the party member sResRef names) dies to sKiller's EffectDamage (nMode 0)
// or EffectDeath (1). nFaction >= 0 moves a new victim to that standard faction first.
void Kill(string sResRef, string sKiller, string sLabel, int nMode = 0, int nFaction = -1)
{
    object pc = GetFirstPC();
    Snap("pc"); Snap("h1"); Snap("h10"); Snap("fam"); Snap("sum");
    object v = GetLocalObject(GetModule(), sResRef);
    int made = !GetIsObjectValid(v);
    if (made)
    {
        vector p = GetPosition(pc);
        v = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(p.x + 2.5, p.y, p.z), 180.0));
        if (nFaction >= 0) ChangeToStandardFaction(v, nFaction);
    }
    SetLocalFloat(GetModule(), "cr", GetChallengeRating(v));
    SendMessageToPC(pc, "KX " + sLabel);
    object k = Killer(sKiller);
    if (sKiller == "none") DelayCommand(0.15, DoKill(v, nMode));
    else DelayCommand(0.15, AssignCommand(k, DoKill(v, nMode)));
    DelayCommand(0.75, Report(v, sLabel, made));
}

void LevelTo(int nTarget, int nClass)
{
    object pc = GetFirstPC();
    if (GetXP(pc) < 780000) SetXP(pc, 780000);
    int guard = 0;
    int cls = nClass == CLASS_TYPE_INVALID ? GetClassByPosition(1, pc) : nClass;
    while (GetHitDice(pc) < nTarget && guard++ < 45)
        if (LevelUpHenchman(pc, cls, TRUE) == 0) break;
    Log("level|target=" + IntToString(nTarget) + "|hd=" + IntToString(GetHitDice(pc)) + "|classes=" + Classes(pc) + "|xp=" + IntToString(GetXP(pc)));
}

void Scale(int n) { SetModuleXPScale(n); Log("scale|set=" + IntToString(n) + "|now=" + IntToString(GetModuleXPScale())); }
void Xp(int n) { SetXP(GetFirstPC(), n); Log("setxp|" + IntToString(n) + "|hd=" + IntToString(GetHitDice(GetFirstPC()))); }

object Npc(string sResRef, float fDx, float fDy, string sKey)
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(p.x + fDx, p.y + fDy, p.z), 0.0), FALSE, sKey);
    SetLocalObject(GetModule(), sKey, o);
    return o;
}

void Hire(string sKey) { AddHenchman(GetFirstPC(), Killer(sKey)); Log("hire|" + sKey + "|master=" + GetName(GetMaster(Killer(sKey)))); }
void Fire(string sKey) { RemoveHenchman(GetFirstPC(), Killer(sKey)); Log("fire|" + sKey); }
void Place(string sKey, float fDx, float fDy)
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    object o = Killer(sKey);
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, JumpToLocation(Location(GetArea(pc), Vector(p.x + fDx, p.y + fDy, p.z), 0.0)));
}
void Familiar() { SummonFamiliar(GetFirstPC()); }
void Summon() { ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire"), GetFirstPC(), 300.0); }
void Unsummon()
{
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, GetFirstPC());
    if (GetIsObjectValid(s)) DestroyObject(s);
    object f = GetAssociate(ASSOCIATE_TYPE_FAMILIAR, GetFirstPC());
    if (GetIsObjectValid(f)) DestroyObject(f);
}
void Who()
{
    object pc = GetFirstPC();
    Log("who|fam=" + GetName(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc)) + "|sum=" + GetName(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc))
        + "|h1=" + GetName(GetMaster(Killer("h1"))) + "|h10=" + GetName(GetMaster(Killer("h10"))));
}

float AllCRs(float t, string sKiller, string sPhase)
{
    int i;
    for (i = 0; i < CR_COUNT; i++)
    {
        DelayCommand(t, Kill("kx" + IntToString(i), sKiller, sPhase + "|kx" + IntToString(i)));
        t += 1.0;
    }
    return t;
}

int ScaleAt(int s)
{
    switch (s)
    {
        case 0: return 0; case 1: return 1; case 2: return 3; case 3: return 5; case 4: return 15;
        case 5: return 33; case 6: return 50; case 7: return 100; case 8: return 200;
    }
    return 250;
}

int LevelAt(int i)
{
    switch (i)
    {
        case 0: return 2; case 1: return 5; case 2: return 9; case 3: return 11; case 4: return 20;
        case 5: return 21; case 6: return 36; case 7: return 37;
    }
    return 40;
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 55.0));
    SetMaxHenchmen(4);
    Log("start|name=" + GetName(pc) + "|race=" + IntToString(GetRacialType(pc)) + "|subrace=" + GetSubRace(pc)
        + "|classes=" + Classes(pc) + "|xp=" + IntToString(GetXP(pc)) + "|scale=" + IntToString(GetModuleXPScale()));
    // Bystanders made now; the first two join the party later.
    object h1 = Npc("kxh1", -3.0, 3.0, "h1");
    object h10 = Npc("kxh10", -3.0, -3.0, "h10");
    object npc = Npc("kxh1", 0.0, 4.0, "npc");
    ChangeToStandardFaction(h1, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(h10, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(npc, STANDARD_FACTION_COMMONER);

    float t = 4.0;
    // 1. Level 1, scale 10, every challenge rating.
    t = AllCRs(t, "pc", "p1");
    // 2. Experience far past level 1 without levelling: does the award follow the level or the experience?
    DelayCommand(t, Xp(200000)); t += 0.5;
    DelayCommand(t, Kill("kx10", "pc", "p2|xp200000|kx10")); t += 1.0;
    DelayCommand(t, Kill("kx15", "pc", "p2|xp200000|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx17", "pc", "p2|xp200000|kx17")); t += 1.0;
    DelayCommand(t, Xp(0)); t += 0.5;
    // 3. Module XP scale (and 250, which should clamp).
    int s;
    for (s = 0; s < 10; s++)
    {
        string n = IntToString(ScaleAt(s));
        DelayCommand(t, Scale(ScaleAt(s))); t += 0.3;
        DelayCommand(t, Kill("kx0", "pc", "p3|scale" + n + "|kx0")); t += 1.0;
        DelayCommand(t, Kill("kx2", "pc", "p3|scale" + n + "|kx2")); t += 1.0;
        DelayCommand(t, Kill("kx10", "pc", "p3|scale" + n + "|kx10")); t += 1.0;
        DelayCommand(t, Kill("kx14", "pc", "p3|scale" + n + "|kx14")); t += 1.0;
    }
    DelayCommand(t, Scale(10)); t += 0.3;
    // 4. Who killed, the victim's faction, and the party: henchmen of levels 1 and 10 (near, then far), a familiar, a summon.
    DelayCommand(t, Kill("kx15", "pc", "p4|alone|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx15", "npc", "p4|bystander_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx15", "mod", "p4|module_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx15", "none", "p4|area_script_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx15", "pc", "p4|pc_effectdeath|kx15", 1)); t += 1.0;
    DelayCommand(t, Kill("kx15", "mod", "p4|module_effectdeath|kx15", 1)); t += 1.0;
    DelayCommand(t, Kill("kx15", "pc", "p4|victim_commoner|kx15", 0, STANDARD_FACTION_COMMONER)); t += 1.0;
    DelayCommand(t, Kill("kx15", "pc", "p4|victim_merchant|kx15", 0, STANDARD_FACTION_MERCHANT)); t += 1.0;
    DelayCommand(t, Kill("kx15", "pc", "p4|victim_defender|kx15", 0, STANDARD_FACTION_DEFENDER)); t += 1.0;
    DelayCommand(t, Hire("h1")); t += 0.5;
    DelayCommand(t, Kill("kx15", "pc", "p4|h1near|pc_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx15", "h1", "p4|h1near|h1_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx14", "pc", "p4|h1near|pc_kills|kx14")); t += 1.0;
    DelayCommand(t, Hire("h10")); t += 0.5;
    DelayCommand(t, Kill("kx15", "pc", "p4|h1h10near|pc_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx15", "h10", "p4|h1h10near|h10_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx18", "pc", "p4|h1h10near|pc_kills|kx18")); t += 1.0;
    DelayCommand(t, Place("h10", 25.0, 0.0)); t += 0.5;
    DelayCommand(t, Kill("kx15", "pc", "p4|h10at25m|pc_kills|kx15")); t += 1.0;
    DelayCommand(t, Place("h10", 50.0, 50.0)); t += 0.5;
    DelayCommand(t, Kill("kx15", "pc", "p4|h10at70m|pc_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx15", "h10", "p4|h10at70m|h10_kills|kx15")); t += 1.0;
    DelayCommand(t, Place("h10", -3.0, -3.0)); t += 0.5;
    DelayCommand(t, Fire("h10")); t += 0.3;
    DelayCommand(t, Kill("kx15", "pc", "p4|h1only_again|pc_kills|kx15")); t += 1.0;
    DelayCommand(t, Fire("h1")); t += 0.3;
    DelayCommand(t, Familiar()); t += 3.0;
    DelayCommand(t, Who()); t += 0.2;
    DelayCommand(t, Kill("kx15", "pc", "p4|familiar|pc_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx15", "fam", "p4|familiar|fam_kills|kx15")); t += 1.0;
    DelayCommand(t, Summon()); t += 3.0;
    DelayCommand(t, Who()); t += 0.2;
    DelayCommand(t, Kill("kx15", "pc", "p4|fam_summon|pc_kills|kx15")); t += 1.0;
    DelayCommand(t, Kill("kx15", "sum", "p4|fam_summon|sum_kills|kx15")); t += 1.0;
    DelayCommand(t, Unsummon()); t += 1.0;
    DelayCommand(t, Kill("kx15", "pc", "p4|after_unsummon|pc_kills|kx15")); t += 1.0;
    // A party member killed by the player (the level 1 henchman rehired, EffectDeath).
    DelayCommand(t, Hire("h1")); t += 0.3;
    DelayCommand(t, Kill("h1", "pc", "p4|kill_own_henchman|h1", 1)); t += 1.0;
    // 5. Player levels in her own class, then a rogue level and three fighter levels for the multiclass penalty.
    int L;
    for (L = 0; L < 9; L++)
    {
        int target = LevelAt(L);
        int cls = L == 7 ? CLASS_TYPE_ROGUE : L == 8 ? CLASS_TYPE_FIGHTER : CLASS_TYPE_INVALID;
        DelayCommand(t, LevelTo(target, cls)); t += 1.0;
        t = AllCRs(t, "pc", "p5|L" + IntToString(target));
    }
    DelayCommand(t + 1.0, VDone());
}
