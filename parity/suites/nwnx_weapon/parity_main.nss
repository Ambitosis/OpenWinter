// NWNX_Weapon: weapon feats given to other base items (focus, epic and greater focus, specialization, epic and greater
// specialization, improved critical, weapon of choice, devastating critical and its event), the options, finesse
// sizes and unarmed weapons (which, on nwserver, doesn't make a weapon finessable), one-and-a-half Strength, ranged
// attack distances. nw_drowfight010 attackers with +20
// attack and five extra attacks fight passive, immortal nw_bandit001 targets; the NWNX_Damage attack event reads
// each attack's bonus, damage and result.
#include "pcombat_inc"
#include "nwnx_damage"
#include "nwnx_weapon"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o); }

object One(string g, string sWeapon, int nFeat1 = -1, int nFeat2 = -1, int nFeat3 = -1)
{
    object a = PPair(g, FIGHTER, TARGET);
    Perm(a, EffectAttackIncrease(20));
    Perm(a, EffectModifyAttacks(5));
    if (nFeat1 >= 0) Perm(a, EffectBonusFeat(nFeat1));
    if (nFeat2 >= 0) Perm(a, EffectBonusFeat(nFeat2));
    if (nFeat3 >= 0) Perm(a, EffectBonusFeat(nFeat3));
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    PUnequip(a, INVENTORY_SLOT_LEFTHAND);
    SetLocalObject(a, "W", PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND));
    return a;
}

void Group(string g, string sWeapon, int nFeat1 = -1, int nFeat2 = -1, int nFeat3 = -1, int nThreat = 0)
{
    int i;
    for (i = 0; i < 3; i++) One(g, sWeapon, nFeat1, nFeat2, nFeat3);
    SetLocalInt(GetModule(), g + ".threat", nThreat);
}

void Archer(string g, string sBow, float y, float fDist)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object a = CreateObject(OBJECT_TYPE_CREATURE, FIGHTER, Location(oArea, Vector(5.0, y, 0.0), 0.0));
    object t = CreateObject(OBJECT_TYPE_CREATURE, TARGET, Location(oArea, Vector(5.0 + fDist, y, 0.0), 180.0));
    PStrip(a); PStrip(t);
    SetLocalString(a, "G", g); SetLocalString(t, "G", g);
    SetLocalObject(a, "T", t); SetLocalObject(t, "A", a);
    ChangeToStandardFaction(a, STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(t, STANDARD_FACTION_COMMONER);
    PEnemies(a, t);
    SetImmortal(t, TRUE); SetImmortal(a, TRUE);
    Perm(a, EffectAttackIncrease(20));
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    PUnequip(a, INVENTORY_SLOT_LEFTHAND);
    PEquip(a, sBow, INVENTORY_SLOT_RIGHTHAND);
    PEquip(a, "nw_wamar001", INVENTORY_SLOT_ARROWS, 99);
}

void Sizes(string sWhen)
{
    PLogI("finesse." + sWhen + ".dagger", NWNX_Weapon_GetWeaponFinesseSize(BASE_ITEM_DAGGER));
    PLogI("finesse." + sWhen + ".rapier", NWNX_Weapon_GetWeaponFinesseSize(BASE_ITEM_RAPIER));
    PLogI("finesse." + sWhen + ".longsword", NWNX_Weapon_GetWeaponFinesseSize(BASE_ITEM_LONGSWORD));
    PLogI("finesse." + sWhen + ".kama", NWNX_Weapon_GetWeaponFinesseSize(BASE_ITEM_KAMA));
    PLogI("finesse." + sWhen + ".bastard", NWNX_Weapon_GetWeaponFinesseSize(BASE_ITEM_BASTARDSWORD));
    PLogI("finesse." + sWhen + ".greatsword", NWNX_Weapon_GetWeaponFinesseSize(BASE_ITEM_GREATSWORD));
}

int Is(int b) { return b ? 1 : 0; }

void Report()
{
    string s = "plain wf wfboth ewf gwf ws ews gws ic woc dcbypass dcon finesse finctl unarmed unarmed2 half half2h rangedef rangeovr ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        s = GetSubString(s, sp + 1, 1000);
        PLogI(g + ".any", Is(PGet(g + ".ev") > 0));
        // Devastating criticals end fights early; archers far away read the original's penalty for an unperceived target.
        if (GetLocalInt(GetModule(), g + ".mod.set") && GetStringLeft(g, 2) != "dc" && GetStringLeft(g, 5) != "range") PLogI(g + ".mod", PGet(g + ".mod"));
        if (PGet(g + ".hits") > 0 && GetStringLeft(g, 5) != "range" && GetStringLeft(g, 2) != "dc") PLogI(g + ".dmin", PGet(g + ".dmin"));
        if (PGet(g + ".threat") > 0) { PLogI(g + ".critwrong", PGet(g + ".critwrong")); PLogI(g + ".threatseen", Is(PGet(g + ".threats") > 0)); }
        if (PGet(g + ".distn") > 0) PLogF(g + ".dist", IntToFloat(PGet(g + ".dist")) / 10.0 / IntToFloat(PGet(g + ".distn")), 1.0);
    }
    string d = "dcbypass dcon ";
    while (d != "")
    {
        int sp = FindSubString(d, " ");
        string g = GetStringLeft(d, sp);
        d = GetSubString(d, sp + 1, 1000);
        int n = PGet(g + ".dcev");
        int crits = PGet(g + ".crits");
        PLogI(g + ".dcany", Is(n > 0));
        PLogI(g + ".dcdata", Is(PGet(g + ".dcweapon") == n) + 2 * Is(PGet(g + ".dctarget") == n) + 4 * Is(PGet(g + ".dcdamage") == n));
        PLogI(g + ".dcbase", Is(PGet(g + ".dcbase") == n));
        PLogI(g + ".dcevery", Is(n == crits));
        PLogI(g + ".res10", Is(PGet(g + ".res10") > 0));
        WriteTimestampedLogEntry("RAW|" + g + ".dc|ev=" + IntToString(n) + " crits=" + IntToString(crits) + " base=" + IntToString(PGet(g + ".dcbase"))
            + " dead=" + IntToString(PGet(g + ".dead")) + " res10=" + IntToString(PGet(g + ".res10")));
    }
    PLogI("dcbypass.dead", PGet("dcbypass.dead"));
    PLogI("dcon.dead", Is(PGet("dcon.dead") > 0));
    PLogI("half.get", NWNX_Weapon_GetOneHalfStrength(GetLocalObject(GetObjectByTag("HALF"), "W")));
    PLogI("half.getplain", NWNX_Weapon_GetOneHalfStrength(GetLocalObject(GetObjectByTag("PLAIN"), "W")));
    PDone();
}

void main()
{
    PClockStart(45.0);
    NWNX_Damage_SetAttackEventScript("nw_atk");
    NWNX_Weapon_SetDevastatingCriticalEventScript("nw_dc");
    Sizes("before");
    NWNX_Weapon_SetWeaponFinesseSize(BASE_ITEM_BASTARDSWORD, CREATURE_SIZE_MEDIUM);
    NWNX_Weapon_SetWeaponUnarmed(BASE_ITEM_GREATSWORD);
    NWNX_Weapon_SetWeaponUnarmed(BASE_ITEM_CLUB);
    Sizes("after");

    NWNX_Weapon_SetWeaponFocusFeat(BASE_ITEM_SHORTSWORD, FEAT_WEAPON_FOCUS_DAGGER);
    NWNX_Weapon_SetEpicWeaponFocusFeat(BASE_ITEM_BATTLEAXE, FEAT_EPIC_WEAPON_FOCUS_DAGGER);
    NWNX_Weapon_SetGreaterWeaponFocusFeat(BASE_ITEM_WARHAMMER, FEAT_ARTIST);
    NWNX_Weapon_SetOption(NWNX_WEAPON_OPT_GRTFOCUS_AB_BONUS, 3);
    NWNX_Weapon_SetWeaponSpecializationFeat(BASE_ITEM_LIGHTMACE, FEAT_WEAPON_SPECIALIZATION_DAGGER);
    NWNX_Weapon_SetEpicWeaponSpecializationFeat(BASE_ITEM_MORNINGSTAR, FEAT_EPIC_WEAPON_SPECIALIZATION_DAGGER);
    NWNX_Weapon_SetGreaterWeaponSpecializationFeat(BASE_ITEM_LIGHTFLAIL, FEAT_BLOODED);
    NWNX_Weapon_SetOption(NWNX_WEAPON_OPT_GRTSPEC_DAM_BONUS, 5);
    NWNX_Weapon_SetWeaponImprovedCriticalFeat(BASE_ITEM_SCIMITAR, FEAT_IMPROVED_CRITICAL_DAGGER);
    NWNX_Weapon_SetWeaponOfChoiceFeat(BASE_ITEM_HANDAXE, FEAT_WEAPON_OF_CHOICE_DAGGER);
    NWNX_Weapon_SetEpicWeaponDevastatingCriticalFeat(BASE_ITEM_KATANA, FEAT_EPIC_DEVASTATING_CRITICAL_DAGGER);
    NWNX_Weapon_SetMaxRangedAttackDistanceOverride(BASE_ITEM_SHORTBOW, 60.0, 30.0, 50.0);

    Group("plain", "nw_wswls001");
    object a = One("plain", "nw_wswls001");
    SetTag(a, "PLAIN");
    Group("wf", "nw_wswss001", FEAT_WEAPON_FOCUS_DAGGER);
    Group("wfboth", "nw_wswss001", FEAT_WEAPON_FOCUS_DAGGER, FEAT_WEAPON_FOCUS_SHORT_SWORD);
    Group("ewf", "nw_waxbt001", FEAT_EPIC_WEAPON_FOCUS_DAGGER);
    Group("gwf", "nw_wblhw001", FEAT_ARTIST);
    Group("ws", "nw_wblml001", FEAT_WEAPON_SPECIALIZATION_DAGGER);
    Group("ews", "nw_wblms001", FEAT_EPIC_WEAPON_SPECIALIZATION_DAGGER);
    Group("gws", "nw_wblfl001", FEAT_BLOODED);
    Group("ic", "nw_wswsc001", FEAT_IMPROVED_CRITICAL_DAGGER, -1, -1, 15);
    Group("woc", "nw_waxhn001", FEAT_WEAPON_OF_CHOICE_DAGGER, FEAT_KI_CRITICAL, FEAT_SUPERIOR_WEAPON_FOCUS, 18);
    Group("dcbypass", "nw_wswka001", FEAT_EPIC_DEVASTATING_CRITICAL_DAGGER, -1, -1, 19);
    Group("dcon", "nw_wswka001", FEAT_EPIC_DEVASTATING_CRITICAL_DAGGER, -1, -1, 19);
    Group("finesse", "nw_wswbs001", FEAT_WEAPON_FINESSE);
    Group("finctl", "nw_wswls001", FEAT_WEAPON_FINESSE);
    Group("unarmed", "nw_wswgs001", FEAT_WEAPON_FINESSE);
    Group("unarmed2", "nw_wblcl001", FEAT_WEAPON_FINESSE);
    int i;
    for (i = 0; i < 3; i++)
    {
        a = One("half", "nw_wswls001");
        SetTag(a, "HALF");
        NWNX_Weapon_SetOneHalfStrength(GetLocalObject(a, "W"), TRUE);
    }
    a = One("half2h", "nw_waxgr001");
    NWNX_Weapon_SetOneHalfStrength(GetLocalObject(a, "W"), TRUE);
    object o = GetFirstObjectInArea(GetObjectByTag("PARITY_AREA"));
    while (GetIsObjectValid(o))
    {
        string g = GetLocalString(o, "G");
        if ((g == "dcbypass" || g == "dcon") && GetIsObjectValid(GetLocalObject(o, "A")))
        {
            SetImmortal(o, FALSE);
            Perm(o, EffectTemporaryHitpoints(1000));
            SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_DEATH, "nw_death");
        }
        if ((g == "finesse" || g == "finctl" || g == "unarmed" || g == "unarmed2") && GetIsObjectValid(GetLocalObject(o, "T"))) Perm(o, EffectAbilityIncrease(ABILITY_DEXTERITY, 12));
        o = GetNextObjectInArea(GetObjectByTag("PARITY_AREA"));
    }
    Archer("rangedef", "nw_wbwln001", 72.0, 50.0);
    Archer("rangeovr", "nw_wbwsh001", 77.0, 55.0);
    PStartAll(3.0);
    DelayCommand(36.0, Report());
}
