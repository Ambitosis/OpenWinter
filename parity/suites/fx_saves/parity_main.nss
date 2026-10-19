// Saving throws and spell resistance as statistics: success rates of WillSave / FortitudeSave /
// ReflexSave at several DCs, save-type bonuses and immunities, GetReflexAdjustedDamage with and
// without evasion, and ResistSpell against spell resistance.
#include "parity_fx"

const int N = 800;

object Make(string sResRef, float fX, float fY)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, PCentre(fX, fY));
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    return o;
}

// The success rate of one save over N rolls: logged as a fraction (sd at most 0.018 per engine).
void Rate(string sKey, object o, int nSave, int nDC, int nType = SAVING_THROW_TYPE_NONE)
{
    int i, nOk = 0, nImmune = 0;
    for (i = 0; i < N; i++)
    {
        int r = nSave == 1 ? FortitudeSave(o, nDC, nType) : nSave == 2 ? ReflexSave(o, nDC, nType) : WillSave(o, nDC, nType);
        if (r == 1) nOk++;
        else if (r == 2) nImmune++;
    }
    PLogF("save." + sKey, IntToFloat(nOk) / IntToFloat(N), 0.09);
    if (nImmune > 0) PLogI("save." + sKey + ".immune", nImmune);
}

void Saves()
{
    object b = Make("nw_bandit001", -8.0, 0.0);
    PLog("bandit.saves", IntToString(GetFortitudeSavingThrow(b)) + " " + IntToString(GetReflexSavingThrow(b)) + " " + IntToString(GetWillSavingThrow(b)));
    Rate("will.10", b, 3, 10);
    Rate("will.16", b, 3, 16);
    Rate("will.21", b, 3, 21);
    Rate("will.1", b, 3, 1);
    Rate("will.40", b, 3, 40);
    Rate("fort.15", b, 1, 15);
    Rate("reflex.12", b, 2, 12);
    Rate("will.0", b, 3, 0);
    Rate("will.300", b, 3, 300);
}

void Types()
{
    object b = Make("nw_bandit001", -4.0, 0.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowIncrease(SAVING_THROW_WILL, 5, SAVING_THROW_TYPE_FEAR), b);
    Rate("fearbonus.fear", b, 3, 16, SAVING_THROW_TYPE_FEAR);
    Rate("fearbonus.mind", b, 3, 16, SAVING_THROW_TYPE_MIND_SPELLS);
    Rate("fearbonus.none", b, 3, 16);
    object m = Make("nw_bandit001", 0.0, 0.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_MIND_SPELLS), m);
    Rate("mindimmune.mind", m, 3, 16, SAVING_THROW_TYPE_MIND_SPELLS);
    Rate("mindimmune.fear", m, 3, 16, SAVING_THROW_TYPE_FEAR);
    Rate("mindimmune.none", m, 3, 16);
    object d = Make("nw_bandit001", 4.0, 0.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_DEATH), d);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_POISON), d);
    Rate("deathimmune.death", d, 1, 16, SAVING_THROW_TYPE_DEATH);
    Rate("poisonimmune.poison", d, 1, 16, SAVING_THROW_TYPE_POISON);
    Rate("poisonimmune.disease", d, 1, 16, SAVING_THROW_TYPE_DISEASE);
    object u = Make("nw_skeleton", 8.0, 0.0);
    Rate("undead.mind", u, 3, 16, SAVING_THROW_TYPE_MIND_SPELLS);
    Rate("undead.poison", u, 1, 16, SAVING_THROW_TYPE_POISON);
    Rate("undead.death", u, 1, 16, SAVING_THROW_TYPE_DEATH);
    Rate("undead.paralysis", u, 1, 16, SAVING_THROW_TYPE_PARALYSIS);
}

void Reflex(string sKey, object o, int nDamage, int nDC)
{
    int i, nSum = 0, nZero = 0, nFull = 0;
    for (i = 0; i < N; i++)
    {
        int d = GetReflexAdjustedDamage(nDamage, o, nDC);
        nSum += d;
        if (d == 0) nZero++;
        if (d == nDamage) nFull++;
    }
    PLogF("reflexdmg." + sKey + ".mean", IntToFloat(nSum) / IntToFloat(N), IntToFloat(nDamage) * 0.06);
    PLogF("reflexdmg." + sKey + ".zero", IntToFloat(nZero) / IntToFloat(N), 0.09);
    PLogF("reflexdmg." + sKey + ".full", IntToFloat(nFull) / IntToFloat(N), 0.09);
}

void ReflexDamage()
{
    object b = Make("nw_bandit001", -8.0, 6.0);
    Reflex("bandit", b, 21, 15);
    object r = Make("nw_drowrogue010", -4.0, 6.0);
    PLog("rogue.evasion", IntToString(GetHasFeat(FEAT_EVASION, r)) + " " + IntToString(GetHasFeat(FEAT_IMPROVED_EVASION, r)) + " ref=" + IntToString(GetReflexSavingThrow(r)));
    Reflex("rogue", r, 21, 25);
    object k = Make("nw_drowrogue010", 0.0, 6.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectParalyze(), k);
    Reflex("rogue.paralyzed", k, 21, 25);
    Reflex("bandit.odd", b, 7, 15);
    Reflex("bandit.one", b, 1, 15);
}

void Resist(string sKey, object oCaster, object oTarget)
{
    int i, nResisted = 0, nGlobe = 0, nMantle = 0;
    for (i = 0; i < N; i++)
    {
        int r = ResistSpell(oCaster, oTarget);
        if (r == 1) nResisted++;
        else if (r == 2) nGlobe++;
        else if (r == 3) nMantle++;
    }
    PLogF("resist." + sKey, IntToFloat(nResisted) / IntToFloat(N), 0.09);
    PLogI("resist." + sKey + ".other", nGlobe + nMantle);
}

void Resistance()
{
    object c = Make("nw_drowmage010", -8.0, 12.0);
    object t = Make("nw_bandit001", -4.0, 12.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellResistanceIncrease(20), t);
    Resist("sr20.mage", c, t);
    object b = Make("nw_bandit001", 0.0, 12.0);
    Resist("sr20.bandit", b, t);
    object n = Make("nw_bandit001", 4.0, 12.0);
    Resist("sr0", c, n);
    object d = Make("nw_drowrogue010", 8.0, 12.0);
    Resist("drow", c, d);
}

void Run()
{
    Saves();
    DelayCommand(0.5, Types());
    DelayCommand(1.0, ReflexDamage());
    DelayCommand(1.5, Resistance());
    DelayCommand(3.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
