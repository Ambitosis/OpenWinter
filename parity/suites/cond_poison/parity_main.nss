// Poisons whose poison.2da row names a script (Script_1 for the initial effect, Script_2 for the secondary), on
// nwserver. Four nw_bandit001 targets per poison with -20 on saves (a natural 20 still saves: counts get a
// tolerance), poisoned by a creature M at 1.0 s. Readings 1.5 s after (initial) and at 63 s (after the secondary),
// counted over the targets:
//   10 Shadow Essence   Script_1 nw_s0_1shadess (EffectAbilityDecrease(STR, -1): nothing lands), secondary 2d6 Str
//   16 Sassone Leaf     Script_1 nw_s0_1sassone (2d12 acid at once)
//   18 Carrion Crawler  Script_1 nw_s0_1carrion (paralysis, linked with an unset effect: it still lands), no secondary
//    8 Blue Whinnis     Script_2 nw_s0_2bluwhinn (sleep for 10 rounds, by the poisoned creature itself)
//   31 Pit Fiend Ichor  Script_2 nw_s0_2pitfiend (death)
#include "parity_fx"

object Of(int nRow, int k) { return GetLocalObject(GetModule(), "T" + IntToString(nRow) + "_" + IntToString(k)); }

effect Find(object o, int nType)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == nType) return e;
        e = GetNextEffect(o);
    }
    return e;
}

int Has(object o, int nType) { return GetIsEffectValid(Find(o, nType)); }

void Count(int nRow, string k, int nType, int bSelfMade = FALSE)
{
    int n = 0, i;
    for (i = 0; i < 4; i++)
    {
        object o = Of(nRow, i);
        effect e = Find(o, nType);
        if (GetIsEffectValid(e) && (!bSelfMade || GetEffectCreator(e) == o)) n++;
    }
    PLogF("p" + IntToString(nRow) + "." + k, IntToFloat(n), 1.0);
}

void Read(int nRow, string t)
{
    int poisoned = 0, dead = 0, hurt = 0, weaker = 0, i;
    int ints1 = -1, decreases = 0;
    for (i = 0; i < 4; i++)
    {
        object o = Of(nRow, i);
        effect p = Find(o, EFFECT_TYPE_POISON);
        if (GetIsEffectValid(p)) { if (ints1 < 0) ints1 = GetEffectInteger(p, 1); }
        // (A creature killed by the poison's script keeps the poison on the original, not here: dead counts too.)
        poisoned += GetIsEffectValid(p) || GetIsDead(o);
        dead += GetIsDead(o);
        hurt += GetIsDead(o) || GetCurrentHitPoints(o) < GetMaxHitPoints(o);
        weaker += GetAbilityScore(o, ABILITY_STRENGTH) < GetLocalInt(o, "STR0");
        decreases += Has(o, EFFECT_TYPE_ABILITY_DECREASE);
    }
    string k = "p" + IntToString(nRow) + "." + t;
    PLogF(k + ".poisoned", IntToFloat(poisoned), 1.0);
    // Sassone Leaf's 2d12 acid kills a 12 hit point bandit or not at random: count harm, not deaths.
    if (nRow != 16) PLogF(k + ".dead", IntToFloat(dead), 1.0);
    PLogF(k + ".harmed", IntToFloat(hurt), 1.0);
    PLogF(k + ".weaker", IntToFloat(weaker), 1.0);
    if (nRow != 16) PLogF(k + ".decreases", IntToFloat(decreases), 1.0);
    if (t == "initial") PLogI(k + ".poisonInt1", ints1);
}

void Poison(int nRow, object o) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectPoison(nRow), o); }

void Row(int i, int nRow)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object m = GetObjectByTag("CP_M");
    int k;
    for (k = 0; k < 4; k++)
    {
        object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(4.0 + IntToFloat(k) * 3.0, 4.0 + IntToFloat(i) * 6.0, 0.0), 0.0));
        int ev;
        for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
        SetAILevel(o, AI_LEVEL_NORMAL);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSavingThrowDecrease(SAVING_THROW_ALL, 50)), o);
        SetLocalInt(o, "STR0", GetAbilityScore(o, ABILITY_STRENGTH));
        SetLocalObject(GetModule(), "T" + IntToString(nRow) + "_" + IntToString(k), o);
        DelayCommand(1.0, AssignCommand(m, Poison(nRow, o)));
    }
    DelayCommand(2.5, Read(nRow, "initial"));
    DelayCommand(63.0, Read(nRow, "secondary"));
}

void main()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(35.0, 35.0, 0.0), 0.0), FALSE, "CP_M");
    Row(0, 10);
    Row(1, 16);
    Row(2, 18);
    Row(3, 8);
    Row(4, 31);
    DelayCommand(2.6, Count(18, "paralysedBySelf", EFFECT_TYPE_PARALYZE, TRUE));
    DelayCommand(63.1, Count(8, "asleepBySelf", EFFECT_TYPE_SLEEP, TRUE));
    DelayCommand(64.0, PDone());
}
