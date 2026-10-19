// Disease after it lands: incubation (disease.2da Incu_Hours game hours), a Fortitude save against Subs_Save at
// the end of incubation and then once a game day, damage and scripts on a failed save, and the cure on the
// second successful save (a failure in between doesn't start the count over).
//
// The module runs at one real minute per game hour (Mod_MinPerHour 1), so incubation takes a minute. A game day
// would take 24 minutes, so each "day" sets the clock to 13:00:50 the next day (SetTime) and lets the 14:00 check
// come on its own a few seconds later.
//
// Saves are random. The creatures that should fail have Fortitude -30 (only a natural 20 saves) and those that
// should save +20 (only a natural 1 fails), and the keys count over groups with a tolerance for those rolls.
#include "parity_fx"

const int BIG = 8;
const int SMALL = 4;

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

void Fort(object o, int nFort)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_SAVING_THROW_DECREASE || GetEffectType(e) == EFFECT_TYPE_SAVING_THROW_INCREASE) RemoveEffect(o, e);
        e = GetNextEffect(o);
    }
    if (nFort > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSavingThrowIncrease(SAVING_THROW_FORT, nFort)), o);
    if (nFort < 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSavingThrowDecrease(SAVING_THROW_FORT, -nFort)), o);
}

object Member(string sGroup, int i) { return GetLocalObject(GetModule(), sGroup + IntToString(i)); }

void FortGroup(string sGroup, int nSize, int nFort)
{
    int i;
    for (i = 0; i < nSize; i++) Fort(Member(sGroup, i), nFort);
}

effect Disease(object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectType(e) == EFFECT_TYPE_DISEASE) return e; e = GetNextEffect(o); }
    return e;
}

void Group(string sGroup, int nSize, int nDisease, int nDur = DURATION_TYPE_PERMANENT, float fDur = 0.0)
{
    int i;
    for (i = 0; i < nSize; i++)
    {
        object o = PFxSpawn(NextCell(), "nw_bandit001");
        int n;
        for (n = 5000; n <= 5012; n++) if (n != 5008) SetEventScript(o, n, "");
        SetLocalObject(GetModule(), sGroup + IntToString(i), o);
        Fort(o, -30);
        // A natural 20 resists it; try again so that every member has it.
        int nTry;
        for (nTry = 0; nTry < 5 && !GetIsEffectValid(Disease(o)); nTry++) ApplyEffectToObject(nDur, EffectDisease(nDisease), o, fDur);
    }
}


// Ability decreases on o: of an ability (-1: any) made by the disease (nMarker 2) or by a script (0).
int Damage(object o, int nAbility, int nMarker)
{
    int n = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_ABILITY_DECREASE && (nAbility < 0 || GetEffectInteger(e, 0) == nAbility) && GetEffectInteger(e, 2) == nMarker) n++;
        e = GetNextEffect(o);
    }
    return n;
}

// What happened to each member since the last report: a failed check (new damage, successful saves as they
// were), a successful one (one more save, no damage), the cure (from one save to none, no damage), death,
// nothing,
// or anything else (counted as "other"; the rules say there is none). Also how many are valid, still have the
// disease and finished incubating. Natural 1s and 20s move a member between failing and saving, hence the
// tolerances on the totals.
void Report(string k, string sGroup, int nSize, float fTol, int bExactOther = TRUE)
{
    int nValid = 0, nHas = 0, nIncubated = 0, nFail = 0, nSave = 0, nCure = 0, nDied = 0, nOther = 0, i;
    for (i = 0; i < nSize; i++)
    {
        object o = Member(sGroup, i);
        if (!GetIsObjectValid(o)) continue;
        nValid++;
        effect e = Disease(o);
        int bHas = GetIsEffectValid(e);
        int nSaves = bHas ? GetEffectInteger(e, 1) : 0;
        int nDamage = Damage(o, -1, 2) + Damage(o, -1, 0);
        if (bHas) { nHas++; nIncubated += GetEffectInteger(e, 2); }
        int bHad = GetLocalInt(o, "had"), nHadSaves = GetLocalInt(o, "saves"), nHadDamage = GetLocalInt(o, "damage");
        int nNew = nDamage - nHadDamage;
        if (!bHad && !bHas && nNew == 0) { }
        else if (bHad && !bHas && GetIsDead(o)) nDied++;
        else if (bHad && !bHas) { if (nHadSaves == 1 && nNew == 0) nCure++; else nOther++; }
        else if (!bHad) nOther++;
        else if (nSaves == nHadSaves && nNew == 0) { }
        else if (nSaves == nHadSaves + 1 && nNew == 0) nSave++;
        else if (nSaves == nHadSaves && nNew > 0) nFail++;
        else nOther++;
        SetLocalInt(o, "had", bHas);
        SetLocalInt(o, "saves", nSaves);
        SetLocalInt(o, "damage", nDamage);
    }
    // Each report: anything the rules don't allow (exact). The rest add up for the end of the run.
    if (bExactOther) PLogI(k + "." + sGroup + ".other", nOther);
    else PLogF(k + "." + sGroup + ".other", IntToFloat(nOther), fTol);
    object m = GetModule();
    SetLocalInt(m, sGroup + ".failed", GetLocalInt(m, sGroup + ".failed") + nFail);
    SetLocalInt(m, sGroup + ".saved", GetLocalInt(m, sGroup + ".saved") + nSave);
    SetLocalInt(m, sGroup + ".cured", GetLocalInt(m, sGroup + ".cured") + nCure);
    SetLocalInt(m, sGroup + ".died", GetLocalInt(m, sGroup + ".died") + nDied);
    SetLocalInt(m, sGroup + ".diseased", nHas);
    SetLocalInt(m, sGroup + ".incubated", nIncubated);
    SetLocalInt(m, sGroup + ".valid", nValid);
}

// The whole run for a group: checks failed and saved, cures and deaths, and how many have it at the end.
void Summary(string sGroup, float fTol)
{
    object m = GetModule();
    PLogF("total." + sGroup + ".failed", IntToFloat(GetLocalInt(m, sGroup + ".failed")), fTol);
    PLogF("total." + sGroup + ".saved", IntToFloat(GetLocalInt(m, sGroup + ".saved")), fTol);
    PLogF("total." + sGroup + ".cured", IntToFloat(GetLocalInt(m, sGroup + ".cured")), fTol);
    PLogF("total." + sGroup + ".died", IntToFloat(GetLocalInt(m, sGroup + ".died")), fTol);
    PLogF("total." + sGroup + ".diseased", IntToFloat(GetLocalInt(m, sGroup + ".diseased")), fTol);
    PLogF("total." + sGroup + ".valid", IntToFloat(GetLocalInt(m, sGroup + ".valid")), fTol);
}

// Damage of each kind on the whole group (the disease's INT and CON, the scripts' points).
void Totals(string k, string sGroup, int nSize, float fTol)
{
    int nInt = 0, nCon = 0, nScript = 0, i;
    for (i = 0; i < nSize; i++)
    {
        object o = Member(sGroup, i);
        nInt += Damage(o, ABILITY_INTELLIGENCE, 2);
        nCon += Damage(o, ABILITY_CONSTITUTION, 2);
        nScript += Damage(o, -1, 0);
    }
    PLogF(k + "." + sGroup + ".int", IntToFloat(nInt), fTol);
    PLogF(k + "." + sGroup + ".con", IntToFloat(nCon), fTol);
    PLogF(k + "." + sGroup + ".script", IntToFloat(nScript), fTol);
}

void ReportAll(string k)
{
    Report(k, "fail", BIG, 0.0);
    Report(k, "save", BIG, 0.0);
    Report(k, "later", BIG, 0.0);
    Report(k, "flip", BIG, 0.0);
    Report(k, "shakes", SMALL, 0.0);
    Report(k, "brief", SMALL, 0.0);
    Totals(k, "shakes", SMALL, 3.0);
    Totals(k, "brief", SMALL, 0.0);
}

// The first member of a group with damage from the disease.
object Damaged(string sGroup, int nSize)
{
    int i;
    for (i = 0; i < nSize; i++) if (Damage(Member(sGroup, i), -1, 2) > 0) return Member(sGroup, i);
    return OBJECT_INVALID;
}

// The first member of a group that still has the disease.
object Diseased(string sGroup, int nSize)
{
    int i;
    for (i = 0; i < nSize; i++) if (GetIsEffectValid(Disease(Member(sGroup, i)))) return Member(sGroup, i);
    return OBJECT_INVALID;
}

int DayNumber()
{
    return (GetCalendarYear() * 12 + GetCalendarMonth() - 1) * 28 + GetCalendarDay() - 1;
}

// When the next check is due, in seconds from now (one game hour is 60 s here).
int DueIn(effect e)
{
    int nNow = (GetTimeHour() * 60 + GetTimeSecond()) * 1000 + GetTimeMillisecond();
    return ((GetEffectInteger(e, 3) - DayNumber()) * 24 * 60000 + GetEffectInteger(e, 4) - nNow + 500) / 1000;
}

// One creature's disease: everything but the dice (and the successful saves, which a natural 1 or 20 changes).
void Detail(string k, object o)
{
    effect e = Disease(o);
    if (!GetIsEffectValid(e)) { PLog(k, "-"); return; }
    PLog(k, IntToString(GetEffectSubType(e)) + ":" + IntToString(GetEffectDurationType(e)) + " disease=" + IntToString(GetEffectInteger(e, 0))
        + " incubated=" + IntToString(GetEffectInteger(e, 2)));
    // Each check comes a few milliseconds after it is due, and the next is due a day after that.
    PLogF(k + ".due", IntToFloat(DueIn(e)), 1.0);
}

// The first disease-made ability decrease on o: everything but the amount rolled.
void DamageShape(string k, object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_ABILITY_DECREASE && GetEffectInteger(e, 2) == 2)
        {
            PLog(k, IntToString(GetEffectSubType(e)) + ":" + IntToString(GetEffectDurationType(e)) + " ability=" + IntToString(GetEffectInteger(e, 0))
                + " amount1to3=" + IntToString(GetEffectInteger(e, 1) >= 1 && GetEffectInteger(e, 1) <= 3) + " sp=" + IntToString(GetEffectSpellId(e)));
            return;
        }
        e = GetNextEffect(o);
    }
    PLog(k, "-");
}

int Dead(string sGroup, int nSize)
{
    int n = 0, i;
    for (i = 0; i < nSize; i++) if (GetIsDead(Member(sGroup, i))) n++;
    return n;
}

int Slaads()
{
    int n = 0;
    object oArea = GetObjectByTag("PARITY_AREA");
    object o = GetFirstObjectInArea(oArea);
    while (GetIsObjectValid(o)) { if (GetStringLowerCase(GetResRef(o)) == "nw_sp_slaadred") n++; o = GetNextObjectInArea(oArea); }
    return n;
}

void Day(int nDay)
{
    if (nDay > 3)
    {
        // Natural 1s and 20s shift a few checks either way.
        Summary("fail", 4.0);
        Summary("save", 4.0);
        Summary("later", 4.0);
        Summary("flip", 4.0);
        Summary("shakes", 2.0);
        Summary("brief", 0.0);
        PDone();
        return;
    }
    string k = "day" + IntToString(nDay);
    // Just before the day's check (due a little after 14:00), then let it come on its own.
    SetTime(13, 0, 50, 0);
    ReportAll(k + ".before");
    Detail(k + ".before.detail", Diseased("fail", BIG));
    // flip: saved at the end of incubation; fails day 1 (damage, but the save still counts); cured on day 2.
    if (nDay == 1) FortGroup("flip", BIG, -30);
    if (nDay == 2) FortGroup("flip", BIG, 20);
    DelayCommand(14.0, ReportAll(k + ".after"));
    DelayCommand(14.0, Detail(k + ".after.detail", Diseased("fail", BIG)));
    DelayCommand(16.0, Day(nDay + 1));
}

void Run()
{
    Group("fail", BIG, DISEASE_FILTH_FEVER);          // Con and Int 1d3 each, DC 12
    Group("save", BIG, DISEASE_FILTH_FEVER);
    Group("later", BIG, DISEASE_FILTH_FEVER);
    Group("flip", BIG, DISEASE_FILTH_FEVER);
    Group("shakes", SMALL, DISEASE_SOLDIER_SHAKES);   // no damage of its own; its daily script takes a point
    Group("slaad", SMALL, DISEASE_RED_SLAAD_EGGS);    // a slaad bursts out when incubation ends
    Group("brief", SMALL, DISEASE_FILTH_FEVER, DURATION_TYPE_TEMPORARY, 40.0); // gone before incubation ends
    Detail("applied.detail", Diseased("fail", BIG));
    // A diseased creature can't catch another disease, the same or a different one.
    object oTwice = Member("brief", 0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDisease(DISEASE_FILTH_FEVER), oTwice);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDisease(DISEASE_MINDFIRE), oTwice);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDisease(DISEASE_SHAKES), oTwice, 40.0);
    int nDiseases = 0;
    effect eTwice = GetFirstEffect(oTwice);
    while (GetIsEffectValid(eTwice)) { if (GetEffectType(eTwice) == EFFECT_TYPE_DISEASE) nDiseases++; eTwice = GetNextEffect(oTwice); }
    PLogI("applied.twice", nDiseases);
    Detail("applied.brief", Diseased("brief", SMALL));
    ReportAll("applied");
    DelayCommand(10.0, FortGroup("save", BIG, 20));
    DelayCommand(10.0, FortGroup("flip", BIG, 20));
    DelayCommand(50.0, ReportAll("t50"));
    DelayCommand(50.0, Detail("t50.detail", Diseased("fail", BIG)));
    DelayCommand(58.0, ReportAll("t58"));
    DelayCommand(65.0, ReportAll("t65"));
    DelayCommand(65.0, Detail("t65.detail.fail", Diseased("fail", BIG)));
    DelayCommand(65.0, Detail("t65.detail.save", Diseased("save", BIG)));
    DelayCommand(65.0, DamageShape("t65.damage", Damaged("fail", BIG)));
    // A slaad for every member that failed (the disease ends; its damage still lands), and only a natural 20 spares one.
    DelayCommand(65.0, PLogF("t65.slaads", IntToFloat(Slaads()), 1.0));
    // Whether a creature the slaad kills keeps the damage varies in the original (its death sometimes strips the
    // effects applied after the killing blow, sometimes not).
    DelayCommand(65.0, Totals("t65", "slaad", SMALL, 2.0));
    DelayCommand(65.0, PLogF("t65.slaad.dead", IntToFloat(Dead("slaad", SMALL)), 1.0));
    DelayCommand(65.0, Report("t65", "slaad", SMALL, 1.0, FALSE));
    // later: failed the end of incubation; saves from then on (cured after day 2).
    DelayCommand(66.0, FortGroup("later", BIG, 20));
    DelayCommand(68.0, Day(1));
}

void main()
{
    DelayCommand(2.0, Run());
}
