#include "visual_inc"
// killxp2: follow-up to killxp. The award follows the level the player's experience gives (not her hit dice), so
// rows of xptable.2da are reached by SetXP on a level 1 player: goblin copies with every whole challenge rating
// (kr0..kr40) are killed with her experience set to a level's threshold first. Then challenge ratings either side
// of the halves (kf0..kf19) at level 1, scale rounding, three and four associates in the party, and the distance
// at which a henchman stops sharing (and whether it is measured from the victim or the player).

const int KF_COUNT = 20; // .45 .49 .5 .51 .6 1.4 1.49 1.5 1.51 2.49 2.5 2.51 3.5 4.5 5.5 20.5 39.5 40.5 .01 -1

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|kx2|" + s); }

object Killer(string sKey)
{
    object pc = GetFirstPC();
    if (sKey == "pc") return pc;
    if (sKey == "fam") return GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc);
    if (sKey == "sum") return GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc);
    return GetLocalObject(GetModule(), sKey);
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

void DoKill(object v) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(20, DAMAGE_TYPE_MAGICAL), v); }

void Report(object v, string sLabel)
{
    object pc = GetFirstPC();
    Log(sLabel + "|cr=" + FloatToString(GetLocalFloat(GetModule(), "cr"), 0, 3) + "|before=" + IntToString(GetLocalInt(GetModule(), "xp_pc"))
        + "|scale=" + IntToString(GetModuleXPScale()) + "|dead=" + IntToString(GetIsDead(v)) + Delta("pc") + Delta("h1") + Delta("h10"));
    DestroyObject(v);
}

// One kill by sKiller's EffectDamage; nSetXp >= 0 sets the player's experience first; the victim stands at
// (fDx, fDy) from the player.
void Kill(string sResRef, string sKiller, string sLabel, int nSetXp = -1, float fDx = 2.5, float fDy = 0.0)
{
    object pc = GetFirstPC();
    if (nSetXp >= 0) SetXP(pc, nSetXp);
    Snap("pc"); Snap("h1"); Snap("h10");
    vector p = GetPosition(pc);
    object v = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(p.x + fDx, p.y + fDy, p.z), 180.0));
    SetLocalFloat(GetModule(), "cr", GetChallengeRating(v));
    SendMessageToPC(pc, "KX " + sLabel);
    DelayCommand(0.15, AssignCommand(Killer(sKiller), DoKill(v)));
    DelayCommand(0.75, Report(v, sLabel));
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
// A rogue level and three fighter levels on the level 1 elf wizard: wizard 1 / rogue 1 / fighter 3 has the
// multiclass penalty (rogue and fighter more than one level apart; wizard is the elf's favoured class).
void Multiclass()
{
    object pc = GetFirstPC();
    SetXP(pc, 10000);
    LevelUpHenchman(pc, CLASS_TYPE_ROGUE, TRUE);
    LevelUpHenchman(pc, CLASS_TYPE_FIGHTER, TRUE);
    LevelUpHenchman(pc, CLASS_TYPE_FIGHTER, TRUE);
    LevelUpHenchman(pc, CLASS_TYPE_FIGHTER, TRUE);
    Log("multiclass|hd=" + IntToString(GetHitDice(pc)) + "|classes=" + Classes(pc) + "|xp=" + IntToString(GetXP(pc)));
}
void Give(int n)
{
    object pc = GetFirstPC();
    SetXP(pc, 10000);
    int before = GetXP(pc);
    GiveXPToCreature(pc, n);
    Log("give|" + IntToString(n) + "|pc=" + IntToString(GetXP(pc) - before) + "|classes=" + Classes(pc));
}

void Scale(int n) { SetModuleXPScale(n); Log("scale|" + IntToString(GetModuleXPScale())); }

object Npc(string sResRef, float fDx, float fDy, string sKey)
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(p.x + fDx, p.y + fDy, p.z), 0.0), FALSE, sKey);
    SetLocalObject(GetModule(), sKey, o);
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    return o;
}
void Hire(string sKey) { AddHenchman(GetFirstPC(), Killer(sKey)); }
void Fire(string sKey) { RemoveHenchman(GetFirstPC(), Killer(sKey)); }
void Place(string sKey, float fDx, float fDy)
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    object o = Killer(sKey);
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, JumpToLocation(Location(GetArea(pc), Vector(p.x + fDx, p.y + fDy, p.z), 0.0)));
}
void Where(string sKey) { Log("where|" + sKey + "|" + FloatToString(GetDistanceBetween(GetFirstPC(), Killer(sKey)), 0, 2)); }
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

int Threshold(int nLevel)
{
    switch (nLevel)
    {
        case 8: return 28000; case 9: return 36000; case 11: return 55000; case 13: return 78000; case 14: return 91000;
        case 18: return 153000; case 19: return 171000; case 20: return 190000; case 21: return 210000;
        case 30: return 435000; case 40: return 780000;
    }
    return 0;
}
int RowLevel(int i)
{
    switch (i)
    {
        case 0: return 9; case 1: return 11; case 2: return 13; case 3: return 14; case 4: return 18; case 5: return 19;
        case 6: return 20; case 7: return 21; case 8: return 30;
    }
    return 40;
}
int RowCR(int i)
{
    switch (i)
    {
        case 0: return 0; case 1: return 4; case 2: return 8; case 3: return 9; case 4: return 12; case 5: return 13;
        case 6: return 17; case 7: return 20; case 8: return 21; case 9: return 29; case 10: return 33;
    }
    return 40;
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 55.0));
    SetMaxHenchmen(4);
    Npc("kxh1", -3.0, 3.0, "h1");
    Npc("kxh10", -3.0, -3.0, "h10");
    Log("start|xp=" + IntToString(GetXP(pc)) + "|hd=" + IntToString(GetHitDice(pc)) + "|scale=" + IntToString(GetModuleXPScale()));

    float t = 4.0;
    int i;
    // 1. Challenge ratings either side of the halves, level 1 (experience 0 before each).
    for (i = 0; i < KF_COUNT; i++) { DelayCommand(t, Kill("kf" + IntToString(i), "pc", "cr|kf" + IntToString(i), 0)); t += 1.0; }
    // 2. Rows of xptable: experience at a level's threshold before each kill.
    int r, c;
    for (r = 0; r < 10; r++)
        for (c = 0; c < 12; c++)
        {
            int L = RowLevel(r), cr = RowCR(c);
            DelayCommand(t, Kill("kr" + IntToString(cr), "pc", "row|L" + IntToString(L) + "|kr" + IntToString(cr), Threshold(L)));
            t += 1.0;
        }
    // One point short of level 9 is still level 8.
    DelayCommand(t, Kill("kr8", "pc", "row|L8max|kr8", 35999)); t += 1.0;
    DelayCommand(t, Kill("kr8", "pc", "row|L8|kr8", 28000)); t += 1.0;
    // 3. Scale rounding on fractional table values (level 9, C8 = 100.5; level 11, C4 = 13.0815; level 9, C0 = 5.25).
    for (i = 0; i < 4; i++)
    {
        int s = i == 0 ? 15 : i == 1 ? 33 : i == 2 ? 7 : 199;
        DelayCommand(t, Scale(s)); t += 0.3;
        DelayCommand(t, Kill("kr8", "pc", "scale|s" + IntToString(s) + "|L9|kr8", 36000)); t += 1.0;
        DelayCommand(t, Kill("kr4", "pc", "scale|s" + IntToString(s) + "|L11|kr4", 55000)); t += 1.0;
        DelayCommand(t, Kill("kr0", "pc", "scale|s" + IntToString(s) + "|L9|kr0", 36000)); t += 1.0;
    }
    DelayCommand(t, Scale(10)); t += 0.3;
    // 4. Three and four associates (level 1, CR 5: 300 alone).
    DelayCommand(t, Kill("kr5", "pc", "party|alone|kr5", 0)); t += 1.0;
    DelayCommand(t, Hire("h1")); t += 0.2;
    DelayCommand(t, Hire("h10")); t += 0.2;
    DelayCommand(t, Familiar()); t += 3.0;
    DelayCommand(t, Who()); t += 0.2;
    DelayCommand(t, Kill("kr5", "pc", "party|h1h10fam|kr5", 0)); t += 1.0;
    DelayCommand(t, Summon()); t += 3.0;
    DelayCommand(t, Who()); t += 0.2;
    DelayCommand(t, Kill("kr5", "pc", "party|h1h10famsum|kr5", 0)); t += 1.0;
    DelayCommand(t, Kill("kr1", "pc", "party|h1h10famsum|kr1", 0)); t += 1.0;
    DelayCommand(t, Unsummon()); t += 1.0;
    DelayCommand(t, Fire("h1")); t += 0.3;
    DelayCommand(t, Who()); t += 0.2;
    DelayCommand(t, Kill("kr5", "pc", "party|h10|kr5", 0)); t += 1.0;
    // 5. The henchman's distance: from the player (victim beside her), then the victim beside the far henchman.
    for (i = 0; i < 8; i++)
    {
        float d = i == 0 ? 20.0 : i == 1 ? 25.0 : i == 2 ? 30.0 : i == 3 ? 35.0 : i == 4 ? 40.0 : i == 5 ? 45.0 : i == 6 ? 50.0 : 55.0;
        string dl = IntToString(FloatToInt(d));
        DelayCommand(t, Place("h10", d, 0.0)); t += 0.6;
        DelayCommand(t, Where("h10")); t += 0.1;
        DelayCommand(t, Kill("kr5", "pc", "dist|h10at" + dl + "|pc_kills_near_pc|kr5", 0)); t += 1.0;
    }
    // h10 at 55 m east: the victim next to it, killed by the player, then by h10.
    DelayCommand(t, Kill("kr5", "pc", "dist|h10at55|pc_kills_near_h10|kr5", 0, 54.0, 1.5)); t += 1.0;
    DelayCommand(t, Kill("kr5", "h10", "dist|h10at55|h10_kills_near_h10|kr5", 0, 54.0, 1.5)); t += 1.0;
    DelayCommand(t, Kill("kr5", "h10", "dist|h10at55|h10_kills_near_pc|kr5", 0)); t += 1.0;
    // Finer: the victim 10 m west of the player, h10 east of the victim at 38..44 m from it (28..34 m from her).
    for (i = 38; i <= 44; i++)
    {
        string vl = IntToString(i);
        DelayCommand(t, Place("h10", IntToFloat(i) - 10.0, 0.0)); t += 0.6;
        DelayCommand(t, Kill("kr5", "pc", "dist|victim10w|h10_from_victim" + vl + "|kr5", 0, -10.0, 0.0)); t += 1.0;
    }
    // 6. The multiclass penalty: kills at level 5 (experience 10000) and GiveXPToCreature.
    DelayCommand(t, Kill("kr5", "pc", "multi|before|kr5", 10000)); t += 1.0;
    DelayCommand(t, Multiclass()); t += 1.0;
    DelayCommand(t, Kill("kr5", "pc", "multi|penalty|kr5", 10000)); t += 1.0;
    DelayCommand(t, Kill("kr3", "pc", "multi|penalty|kr3", 10000)); t += 1.0;
    DelayCommand(t, Kill("kr1", "pc", "multi|penalty|kr1", 10000)); t += 1.0;
    DelayCommand(t, Give(100)); t += 0.5;
    DelayCommand(t, Give(7)); t += 0.5;
    DelayCommand(t + 1.0, VDone());
}
