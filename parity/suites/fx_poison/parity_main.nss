// Poison and disease: the save when they are applied, what lands, the initial and secondary ability
// damage of poison a minute later, and how long the effects stay. Saves (and the dice) are random, so
// most keys count over groups of ten creatures that can (almost) never save, or never fail.
#include "parity_fx"

const int G = 10;

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

object Make(int nFort)
{
    object o = PFxSpawn(NextCell(), "nw_bandit001");
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    if (nFort > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSavingThrowIncrease(SAVING_THROW_FORT, nFort)), o);
    if (nFort < 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSavingThrowDecrease(SAVING_THROW_FORT, -nFort)), o);
    return o;
}

// A group of G creatures with the effect applied.
void Group(string sGroup, int nFort, effect e, int nDur = DURATION_TYPE_PERMANENT, float fDur = 0.0)
{
    int i;
    for (i = 0; i < G; i++)
    {
        object o = Make(nFort);
        SetLocalObject(GetModule(), sGroup + IntToString(i), o);
        ApplyEffectToObject(nDur, e, o, fDur);
    }
}

// How many of the group have an effect of the type (and, for ability decreases, of the ability).
int Count(string sGroup, int nType, int nAbility = -1, int nDurType = -1)
{
    int i, n = 0;
    for (i = 0; i < G; i++)
    {
        object o = GetLocalObject(GetModule(), sGroup + IntToString(i));
        effect e = GetFirstEffect(o);
        int bHas = FALSE;
        while (GetIsEffectValid(e))
        {
            if (GetEffectType(e, TRUE) == nType && (nAbility < 0 || GetEffectInteger(e, 0) == nAbility)
                && (nDurType < 0 || GetEffectDurationType(e) == nDurType)) bHas = TRUE;
            e = GetNextEffect(o);
        }
        if (bHas) n++;
    }
    return n;
}

// Integers of the first effect of the type in the group: the deterministic ones only.
string First(string sGroup, int nType, string sWhich)
{
    int i;
    for (i = 0; i < G; i++)
    {
        object o = GetLocalObject(GetModule(), sGroup + IntToString(i));
        effect e = GetFirstEffect(o);
        while (GetIsEffectValid(e))
        {
            if (GetEffectType(e, TRUE) == nType)
            {
                string s = IntToString(GetEffectSubType(e)) + ":" + IntToString(GetEffectDurationType(e)) + ":" + PFxCreator(e) + "[";
                int n;
                for (n = 0; n < 4; n++) s += (FindSubString(sWhich, IntToString(n)) >= 0 ? IntToString(GetEffectInteger(e, n)) : "r") + ",";
                return s + "]";
            }
            e = GetNextEffect(o);
        }
    }
    return "-";
}

// Nine or ten of ten land on a natural 20 aside.
void LogCount(string sKey, int n, float fTol = 2.0) { PLogF(sKey, IntToFloat(n), fTol); }

void Early()
{
    LogCount("nightshade.fail.poisoned", Count("nf", EFFECT_TYPE_POISON));
    LogCount("nightshade.fail.dexdamage", Count("nf", EFFECT_TYPE_ABILITY_DECREASE, ABILITY_DEXTERITY));
    PLog("nightshade.fail.poison", First("nf", EFFECT_TYPE_POISON, "013"));
    PLog("nightshade.fail.initial", First("nf", EFFECT_TYPE_ABILITY_DECREASE, "012"));
    LogCount("nightshade.save.poisoned", Count("ns", EFFECT_TYPE_POISON));
    LogCount("bladebane.fail.strdamage", Count("bf", EFFECT_TYPE_ABILITY_DECREASE, ABILITY_STRENGTH));
    LogCount("twice.centipede", Count("tw", EFFECT_TYPE_POISON));
    LogCount("temporary.poisoned", Count("tp", EFFECT_TYPE_POISON, -1, DURATION_TYPE_TEMPORARY));
    PLog("temporary.initial", First("tp", EFFECT_TYPE_ABILITY_DECREASE, "012"));
    LogCount("instant.poisoned.permanent", Count("in", EFFECT_TYPE_POISON, -1, DURATION_TYPE_PERMANENT));
    LogCount("filth.fail.diseased", Count("df", EFFECT_TYPE_DISEASE));
    PLog("filth.fail.disease", First("df", EFFECT_TYPE_DISEASE, "012"));
    LogCount("filth.save.diseased", Count("ds", EFFECT_TYPE_DISEASE));
}

void Middle()
{
    LogCount("nightshade.fail.poisoned.t30", Count("nf", EFFECT_TYPE_POISON));
    LogCount("temporary.anything.t25", Count("tp", EFFECT_TYPE_ABILITY_DECREASE) + Count("tp", EFFECT_TYPE_POISON));
}

void Late()
{
    LogCount("nightshade.fail.poisoned.t62", Count("nf", EFFECT_TYPE_POISON));
    // The second save fails nine times in ten as well; one fewer for each first save that succeeded.
    LogCount("nightshade.fail.condamage.t62", Count("nf", EFFECT_TYPE_ABILITY_DECREASE, ABILITY_CONSTITUTION), 2.5);
    PLog("nightshade.fail.secondary", First("nf", EFFECT_TYPE_ABILITY_DECREASE, "02"));
    LogCount("secondsave.condamage.t62", Count("sv", EFFECT_TYPE_ABILITY_DECREASE, ABILITY_CONSTITUTION));
    LogCount("bladebane.fail.condamage.t62", Count("bf", EFFECT_TYPE_ABILITY_DECREASE, ABILITY_CONSTITUTION), 2.5);
    LogCount("instant.condamage.t62", Count("in", EFFECT_TYPE_ABILITY_DECREASE, ABILITY_CONSTITUTION), 2.5);
    LogCount("filth.fail.diseased.t62", Count("df", EFFECT_TYPE_DISEASE));
}

// Swaps the save penalty for the largest bonus (+20, the limit): only a natural 1 fails now.
void Strengthen(string sGroup)
{
    int i;
    for (i = 0; i < G; i++)
    {
        object o = GetLocalObject(GetModule(), sGroup + IntToString(i));
        effect e = GetFirstEffect(o);
        while (GetIsEffectValid(e)) { if (GetEffectType(e) == EFFECT_TYPE_SAVING_THROW_DECREASE) RemoveEffect(o, e); e = GetNextEffect(o); }
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSavingThrowIncrease(SAVING_THROW_FORT, 20)), o);
    }
}

void Run()
{
    Group("nf", -30, EffectPoison(POISON_NIGHTSHADE));
    Group("ns", 40, EffectPoison(POISON_NIGHTSHADE));
    Group("bf", -30, EffectPoison(POISON_BLADE_BANE));
    Group("sv", -30, EffectPoison(POISON_NIGHTSHADE));
    DelayCommand(10.0, Strengthen("sv"));
    Group("tw", -30, EffectPoison(POISON_NIGHTSHADE));
    int i;
    for (i = 0; i < G; i++) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectPoison(POISON_SMALL_CENTIPEDE_POISON), GetLocalObject(GetModule(), "tw" + IntToString(i)));
    Group("tp", -30, EffectPoison(POISON_NIGHTSHADE), DURATION_TYPE_TEMPORARY, 20.0);
    Group("in", -30, EffectPoison(POISON_NIGHTSHADE), DURATION_TYPE_INSTANT);
    Group("df", -30, EffectDisease(DISEASE_FILTH_FEVER));
    Group("ds", 40, EffectDisease(DISEASE_FILTH_FEVER));
    DelayCommand(0.5, Early());
    DelayCommand(25.0, Middle());
    DelayCommand(62.0, Late());

    // Landing rates with the plain fortitude save (+3 against DC 10 and 12).
    int nPoison = 0, nDisease = 0;
    for (i = 0; i < 40; i++)
    {
        object x = Make(0);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectPoison(POISON_NIGHTSHADE), x);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDisease(DISEASE_FILTH_FEVER), x);
        effect e = GetFirstEffect(x);
        while (GetIsEffectValid(e))
        {
            if (GetEffectType(e) == EFFECT_TYPE_POISON) nPoison++;
            if (GetEffectType(e) == EFFECT_TYPE_DISEASE) nDisease++;
            e = GetNextEffect(x);
        }
        DestroyObject(x, 0.5);
    }
    // Lands on a failed save: 30% and 40%; sd over 40 about 0.075 per engine.
    PLogF("poison.landed", IntToFloat(nPoison) / 40.0, 0.3);
    PLogF("disease.landed", IntToFloat(nDisease) / 40.0, 0.3);
    DelayCommand(63.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
