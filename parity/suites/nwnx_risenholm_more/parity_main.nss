// NWNX_Risenholm in more detail: which effects RemoveEffectsByTag takes and counts, displayed heads of a
// part-based creature, and the level history of creatures with several classes.
#include "parity_inc"
#include "nwnx_risenholm"

string List(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        s += IntToString(GetEffectType(e)) + ":" + GetEffectTag(e) + ";";
        e = GetNextEffect(o);
    }
    return s;
}

string History(object o)
{
    json j = NWNX_Risenholm_GetLevelHistory(o);
    json jLevels = JsonObjectGet(j, "levels");
    int i;
    // Skill points are left out: for creatures without a stored history NWNX reports an uninitialised value.
    for (i = 0; i < JsonGetLength(jLevels); i++)
        jLevels = JsonArraySet(jLevels, i, JsonObjectDel(JsonArrayGet(jLevels, i), "skillpoints"));
    return JsonDump(JsonObjectSet(j, "levels", jLevels));
}

void main()
{
    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre());
    int e;
    for (e = 5000; e <= 5012; e++) SetEventScript(oGob, e, "");
    PLog("fx.start", List(oGob));
    effect eLink = EffectLinkEffects(TagEffect(EffectSkillIncrease(SKILL_HIDE, 2), "INNER"), EffectSavingThrowIncrease(SAVING_THROW_ALL, 1));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(eLink, "OUTER"), oGob);
    PLog("fx.link", List(oGob));
    effect eLink2 = EffectLinkEffects(EffectACIncrease(1), EffectAttackIncrease(1));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, eLink2, oGob);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(2), "SOLO"), oGob);
    PLog("fx.all", List(oGob));
    PLogI("fx.has.inner", NWNX_Risenholm_GetHasEffectByTag(oGob, "INNER"));
    PLogI("fx.has.outer", NWNX_Risenholm_GetHasEffectByTag(oGob, "OUTER"));
    PLogI("fx.remove.outer", NWNX_Risenholm_RemoveEffectsByTag(oGob, "OUTER"));
    PLog("fx.after.outer", List(oGob));
    // The count for an empty tag includes the engine's hidden effects (nwnx_risenholm_fx), so it is not compared.
    NWNX_Risenholm_RemoveEffectsByTag(oGob, "");
    PLog("fx.after.empty", List(oGob));
    PLogI("fx.remove.solo", NWNX_Risenholm_RemoveEffectsByTag(oGob, "SOLO"));
    PLog("fx.after.solo", List(oGob));

    object oElf = CreateObject(OBJECT_TYPE_CREATURE, "nw_elfmage015", PCentre(3.0));
    for (e = 5000; e <= 5012; e++) SetEventScript(oElf, e, "");
    PLogI("elf.head", GetCreatureBodyPart(CREATURE_PART_HEAD, oElf));
    PLogI("elf.displayed", NWNX_Risenholm_GetDisplayedHead(oElf));
    NWNX_Risenholm_SetDisplayedHead(oElf, 1044);
    PLogI("elf.displayed.set", NWNX_Risenholm_GetDisplayedHead(oElf));
    SetCreatureBodyPart(CREATURE_PART_HEAD, 3, oElf);
    PLogI("elf.afterpart", NWNX_Risenholm_GetDisplayedHead(oElf));
    PLogI("elf.afterpart.real", GetCreatureBodyPart(CREATURE_PART_HEAD, oElf));
    NWNX_Risenholm_SetDisplayedHead(oElf, 7);
    NWNX_Risenholm_SetDisplayedHead(oElf, 0);
    PLogI("elf.reset", NWNX_Risenholm_GetDisplayedHead(oElf));
    PLog("elf.history", History(oElf));
    object oBandit = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(-3.0));
    PLogI("bandit.head", NWNX_Risenholm_GetDisplayedHead(oBandit));
    PLog("bandit.history", History(oBandit));
    object oCleric = CreateObject(OBJECT_TYPE_CREATURE, "nw_clerichu001", PCentre(-6.0));
    PLog("cleric.history", History(oCleric));
    PDone();
}
