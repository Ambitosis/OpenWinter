// NWNX_SkillRanks: the engine's skill feat table as the plugin reports it, and how changes to it, area modifiers and
// the blindness penalty feed GetSkillRank.
#include "parity_inc"
#include "nwnx_skillranks"

string Feat(struct NWNX_SkillRanks_SkillFeat f)
{
    return IntToString(f.iFeat) + " m" + IntToString(f.iModifier) + " f" + IntToString(f.iFocusFeat) + " c" + f.sClasses
        + " l" + FloatToString(f.fClassLevelMod, 0, 2) + " r" + IntToString(f.iAreaFlagsRequired) + " x" + IntToString(f.iAreaFlagsForbidden)
        + " d" + IntToString(f.iDayOrNight) + " a" + IntToString(f.bBypassArmorCheckPenalty) + " k" + IntToString(f.iKeyAbilityMask);
}

void Table()
{
    int s;
    for (s = 0; s < 30; s++)
    {
        int n = NWNX_SkillRanks_GetSkillFeatCountForSkill(s);
        PLogI("table." + IntToString(s) + ".count", n);
        int i;
        // Keyed by feat: the order of each skill's list is the engine's own.
        for (i = 0; i < n; i++)
        {
            struct NWNX_SkillRanks_SkillFeat f = NWNX_SkillRanks_GetSkillFeatForSkillByIndex(s, i);
            PLog("table." + IntToString(s) + ".f" + IntToString(f.iFeat), Feat(f));
        }
    }
}

object Spawn(string sRes, float x)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, PCentre(x, 0.0));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    return o;
}

void Ranks(string sWhen, object o)
{
    int s;
    string v = "";
    for (s = 0; s < 28; s++) v += IntToString(GetSkillRank(s, o)) + " ";
    PLog("rank." + sWhen, v);
}

void main()
{
    Table();
    PLogI("blind.default", NWNX_SkillRanks_GetBlindnessPenalty());
    object oArea = GetObjectByTag("PARITY_AREA");
    PLogI("area.default", NWNX_SkillRanks_GetAreaModifier(oArea, SKILL_LORE));
    PLogI("area.flags", GetIsAreaInterior(oArea) + 2 * GetIsAreaNatural(oArea) + 4 * GetIsAreaAboveGround(oArea));

    object a = Spawn("nw_drowfight010", 0.0);
    object b = Spawn("nw_bandit001", 2.0);
    Ranks("a.before", a);
    Ranks("b.before", b);
    int ab;
    string mods = "";
    for (ab = 0; ab < 6; ab++) mods += IntToString(GetAbilityModifier(ab, a)) + " ";
    PLog("a.mods", mods);
    PLog("a.levels", IntToString(GetHitDice(a)) + " " + IntToString(GetClassByPosition(1, a)) + " " + IntToString(GetClassByPosition(2, a)));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(18), b);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(586), b);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_TRACKLESS_STEP), b);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_STONECUNNING), b);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_BARDIC_KNOWLEDGE), b);
    Ranks("b.feats", b);

    // Area modifier.
    NWNX_SkillRanks_SetAreaModifier(oArea, SKILL_LORE, 10);
    PLogI("area.set", NWNX_SkillRanks_GetAreaModifier(oArea, SKILL_LORE));
    Ranks("a.area", a);

    // Skill focus modifiers.
    NWNX_SkillRanks_SetSkillFeatFocusModifier(5);
    NWNX_SkillRanks_SetSkillFeatFocusModifier(12, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_SKILL_FOCUS_HEAL), a);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_EPIC_SKILL_FOCUS_HEAL), a);
    Ranks("a.focus", a);
    PLog("table.focusheal", Feat(NWNX_SkillRanks_GetSkillFeat(SKILL_HEAL, FEAT_SKILL_FOCUS_HEAL)));

    // A new skill feat: Artist gives +3 Heal, plus half the fighter levels, using the better of Int and Wis.
    struct NWNX_SkillRanks_SkillFeat f;
    f.iSkill = SKILL_HEAL;
    f.iFeat = FEAT_ARTIST;
    f.iModifier = 3;
    f.fClassLevelMod = 0.5;
    f = NWNX_SkillRanks_AddSkillFeatClass(f, CLASS_TYPE_FIGHTER);
    f.iKeyAbilityMask = NWNX_SKILLRANKS_KEY_ABILITY_INTELLIGENCE | NWNX_SKILLRANKS_KEY_ABILITY_WISDOM | NWNX_SKILLRANKS_KEY_ABILITY_CALC_MAX;
    NWNX_SkillRanks_SetSkillFeat(f, TRUE);
    PLogI("new.count", NWNX_SkillRanks_GetSkillFeatCountForSkill(SKILL_HEAL));
    PLog("new.get", Feat(NWNX_SkillRanks_GetSkillFeat(SKILL_HEAL, FEAT_ARTIST)));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_ARTIST), a);
    Ranks("a.artist", a);
    // Not created when it doesn't exist and createIfNonExistent is FALSE.
    f.iFeat = FEAT_BLOODED;
    NWNX_SkillRanks_SetSkillFeat(f);
    PLogI("new.nocreate", NWNX_SkillRanks_GetSkillFeatCountForSkill(SKILL_HEAL));
    // Armour check bypass and an all-classes level modifier on Hide.
    struct NWNX_SkillRanks_SkillFeat h;
    h.iSkill = SKILL_HIDE;
    h.iFeat = FEAT_BLOODED;
    h.iModifier = 1;
    h.fClassLevelMod = 1.0;
    h.bBypassArmorCheckPenalty = TRUE;
    NWNX_SkillRanks_SetSkillFeat(h, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_BLOODED), a);
    Ranks("a.blooded", a);
    // Sum, min and average key abilities on Spot.
    struct NWNX_SkillRanks_SkillFeat k;
    k.iSkill = SKILL_SPOT;
    k.iFeat = FEAT_ARTIST;
    k.iKeyAbilityMask = NWNX_SKILLRANKS_KEY_ABILITY_STRENGTH | NWNX_SKILLRANKS_KEY_ABILITY_DEXTERITY | NWNX_SKILLRANKS_KEY_ABILITY_CALC_SUM;
    NWNX_SkillRanks_SetSkillFeat(k, TRUE);
    k.iSkill = SKILL_LISTEN;
    k.iKeyAbilityMask = NWNX_SKILLRANKS_KEY_ABILITY_STRENGTH | NWNX_SKILLRANKS_KEY_ABILITY_CHARISMA | NWNX_SKILLRANKS_KEY_ABILITY_CALC_MIN;
    NWNX_SkillRanks_SetSkillFeat(k, TRUE);
    k.iSkill = SKILL_SEARCH;
    k.iKeyAbilityMask = NWNX_SKILLRANKS_KEY_ABILITY_STRENGTH | NWNX_SKILLRANKS_KEY_ABILITY_DEXTERITY | NWNX_SKILLRANKS_KEY_ABILITY_CALC_AVERAGE;
    NWNX_SkillRanks_SetSkillFeat(k, TRUE);
    Ranks("a.keys", a);
    // Area-restricted: required interior / forbidden natural, on Lore.
    struct NWNX_SkillRanks_SkillFeat r;
    r.iSkill = SKILL_LORE;
    r.iFeat = FEAT_BLOODED;
    r.iModifier = 4;
    r.iAreaFlagsRequired = 4;
    NWNX_SkillRanks_SetSkillFeat(r, TRUE);
    r.iSkill = SKILL_APPRAISE;
    r.iAreaFlagsRequired = 0;
    r.iAreaFlagsForbidden = 4;
    NWNX_SkillRanks_SetSkillFeat(r, TRUE);
    r.iSkill = SKILL_PERSUADE;
    r.iAreaFlagsForbidden = 0;
    r.iDayOrNight = GetIsDay() ? 1 : 2;
    NWNX_SkillRanks_SetSkillFeat(r, TRUE);
    r.iSkill = SKILL_BLUFF;
    r.iDayOrNight = GetIsDay() ? 2 : 1;
    NWNX_SkillRanks_SetSkillFeat(r, TRUE);
    Ranks("a.areaflags", a);

    // Blindness.
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBlindness(), b);
    Ranks("b.blind4", b);
    NWNX_SkillRanks_SetBlindnessPenalty(10);
    PLogI("blind.set", NWNX_SkillRanks_GetBlindnessPenalty());
    Ranks("b.blind10", b);
    PDone();
}
