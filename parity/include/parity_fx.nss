// Effects parity helpers: stat snapshots, effect descriptions and effect lists.
#include "parity_inc"

const int PFX_STATS = 31;

string PFxStatName(int i)
{
    switch (i)
    {
        case 0: return "ac";
        case 1: return "fort";
        case 2: return "ref";
        case 3: return "will";
        case 4: return "str";
        case 5: return "dex";
        case 6: return "con";
        case 7: return "int";
        case 8: return "wis";
        case 9: return "cha";
        case 10: return "spot";
        case 11: return "hide";
        case 12: return "disc";
        case 13: return "conc";
        case 14: return "sr";
        case 15: return "maxhp";
        case 16: return "hp";
        case 17: return "mrate";
        case 18: return "immMind";
        case 19: return "immPoison";
        case 20: return "immSleep";
        case 21: return "immDeath";
        case 22: return "cmd";
        case 23: return "dead";
        case 24: return "size";
        case 25: return "app";
        case 26: return "bab";
        case 27: return "hd";
        case 28: return "featAlert";
        case 29: return "turnres";
        case 30: return "listen";
    }
    return "?";
}

int PFxStat(object o, int i)
{
    switch (i)
    {
        case 0: return GetAC(o);
        case 1: return GetFortitudeSavingThrow(o);
        case 2: return GetReflexSavingThrow(o);
        case 3: return GetWillSavingThrow(o);
        case 4: return GetAbilityScore(o, ABILITY_STRENGTH);
        case 5: return GetAbilityScore(o, ABILITY_DEXTERITY);
        case 6: return GetAbilityScore(o, ABILITY_CONSTITUTION);
        case 7: return GetAbilityScore(o, ABILITY_INTELLIGENCE);
        case 8: return GetAbilityScore(o, ABILITY_WISDOM);
        case 9: return GetAbilityScore(o, ABILITY_CHARISMA);
        case 10: return GetSkillRank(SKILL_SPOT, o);
        case 11: return GetSkillRank(SKILL_HIDE, o);
        case 12: return GetSkillRank(SKILL_DISCIPLINE, o);
        case 13: return GetSkillRank(SKILL_CONCENTRATION, o);
        case 14: return GetSpellResistance(o);
        case 15: return GetMaxHitPoints(o);
        case 16: return GetCurrentHitPoints(o);
        case 17: return GetMovementRate(o);
        case 18: return GetIsImmune(o, IMMUNITY_TYPE_MIND_SPELLS);
        case 19: return GetIsImmune(o, IMMUNITY_TYPE_POISON);
        case 20: return GetIsImmune(o, IMMUNITY_TYPE_SLEEP);
        case 21: return GetIsImmune(o, IMMUNITY_TYPE_DEATH);
        case 22: return GetCommandable(o);
        case 23: return GetIsDead(o);
        case 24: return GetCreatureSize(o);
        case 25: return GetAppearanceType(o);
        case 26: return GetBaseAttackBonus(o);
        case 27: return GetHitDice(o);
        case 28: return GetHasFeat(FEAT_ALERTNESS, o);
        case 29: return GetTurnResistanceHD(o);
        case 30: return GetSkillRank(SKILL_LISTEN, o);
    }
    return 0;
}

// Remembers every stat of o (on o itself) for PFxDiff.
void PFxSnap(object o)
{
    int i;
    for (i = 0; i < PFX_STATS; i++) SetLocalInt(o, "pfx_snap" + IntToString(i), PFxStat(o, i));
}

// The stats that changed since PFxSnap, as "ac+2 str-4" ("-" when nothing changed).
string PFxDiff(object o)
{
    string s = "";
    int i;
    for (i = 0; i < PFX_STATS; i++)
    {
        int d = PFxStat(o, i) - GetLocalInt(o, "pfx_snap" + IntToString(i));
        if (d == 0) continue;
        if (s != "") s += " ";
        s += PFxStatName(i) + (d > 0 ? "+" : "") + IntToString(d);
    }
    return s == "" ? "-" : s;
}

string PFxCreator(effect e)
{
    object c = GetEffectCreator(e);
    if (!GetIsObjectValid(c)) return "inv";
    if (c == GetModule()) return "mod";
    if (c == OBJECT_SELF) return "self";
    return GetTag(c) == "" ? "obj" : GetTag(c);
}

// The effect's eight integers, comma separated. Some effects keep their next tick as a calendar day
// and time (regeneration and run-script in integers 2 and 3, disease in 3 and 4, poison in 4 and 5);
// those show as "t".
string PFxInts(effect e)
{
    int nType = GetEffectType(e, TRUE);
    int nTime = nType == EFFECT_TYPE_REGENERATE || nType == EFFECT_TYPE_RUNSCRIPT ? 2
        : nType == EFFECT_TYPE_DISEASE ? 3 : nType == EFFECT_TYPE_POISON ? 4 : -1;
    string s = IntToString(GetEffectInteger(e, 0));
    int i;
    for (i = 1; i < 8; i++) s += "," + (nTime > 0 && (i == nTime || i == nTime + 1) ? "t" : IntToString(GetEffectInteger(e, i)));
    return s;
}

// Everything the getters report about an effect.
string PFxDescribe(effect e)
{
    return "valid=" + IntToString(GetIsEffectValid(e))
        + " t=" + IntToString(GetEffectType(e)) + "/" + IntToString(GetEffectType(e, TRUE))
        + " st=" + IntToString(GetEffectSubType(e))
        + " dt=" + IntToString(GetEffectDurationType(e))
        + " d=" + IntToString(GetEffectDuration(e)) + "/" + IntToString(GetEffectDurationRemaining(e))
        + " sp=" + IntToString(GetEffectSpellId(e))
        + " cl=" + IntToString(GetEffectCasterLevel(e))
        + " cr=" + PFxCreator(e)
        + " tag=" + GetEffectTag(e)
        + " i=" + PFxInts(e);
}

// A short description for effect lists.
string PFxShort(effect e)
{
    return IntToString(GetEffectType(e, TRUE)) + ":" + IntToString(GetEffectSubType(e)) + ":" + IntToString(GetEffectDurationType(e))
        + ":" + IntToString(GetEffectSpellId(e)) + "[" + PFxInts(e) + "]";
}

// Every effect on o, in iteration order.
string PFxList(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (s != "") s += " ";
        s += PFxShort(e);
        e = GetNextEffect(o);
    }
    return s == "" ? "-" : s;
}

int PFxCount(object o)
{
    int n = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { n++; e = GetNextEffect(o); }
    return n;
}

// Types on o in iteration order ("38,40").
string PFxTypes(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (s != "") s += ",";
        s += IntToString(GetEffectType(e, TRUE));
        e = GetNextEffect(o);
    }
    return s == "" ? "-" : s;
}

// A fresh test creature at a grid cell (0..143) of the test area.
object PFxSpawn(int nCell, string sResRef = "nw_bandit001")
{
    float x = -16.5 + IntToFloat(nCell % 12) * 3.0;
    float y = -16.5 + IntToFloat(nCell / 12) * 3.0;
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, PCentre(x, y));
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}
