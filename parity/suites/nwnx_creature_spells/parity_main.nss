// NWNX_Creature spell state: slots (spontaneous and prepared), bonus spells, caster level modifiers and overrides,
// item caster level, spell resistance, and the casts NWNX starts (DoItemCastSpell, AddCastSpellActions).
#include "parity_inc"
#include "nwnx_creature"

void Slots(string k, object o, int nClass, int nPos, int nTo)
{
    string m = "", r = "", b = "";
    int i;
    for (i = 0; i <= nTo; i++)
    {
        m += IntToString(NWNX_Creature_GetMaxSpellSlots(o, nClass, i)) + " ";
        r += IntToString(NWNX_Creature_GetRemainingSpellSlots(o, nClass, i)) + " ";
        b += IntToString(NWNX_Creature_GetNumberOfBonusSpells(o, nPos, i)) + " ";
    }
    PLog(k + ".max", m);
    PLog(k + ".left", r);
    PLog(k + ".bonus", b);
}

object Caster(string sTag, float fDx)
{
    return CreateObject(OBJECT_TYPE_CREATURE, "nw_lich001", PCentre(fDx, 6.0), FALSE, sTag);
}

void Casts()
{
    object a = Caster("acs_plain", -6.0);
    object b = Caster("acs_mod", -4.0);
    object c = Caster("acs_over", -2.0);
    object d = Caster("acs_cheat", 0.0);
    object e = Caster("dics_plain", 2.0);
    object f = Caster("dics_mod", 4.0);
    object g = Caster("acs_queued", 6.0);
    NWNX_Creature_SetCasterLevelModifier(b, CLASS_TYPE_WIZARD, 3);
    NWNX_Creature_SetCasterLevelOverride(c, CLASS_TYPE_WIZARD, 5);
    NWNX_Creature_SetCasterLevelModifier(f, CLASS_TYPE_WIZARD, 3);
    PLog("clm.read", IntToString(NWNX_Creature_GetCasterLevelModifier(b, CLASS_TYPE_WIZARD)) + " "
        + IntToString(NWNX_Creature_GetCasterLevelModifier(b, CLASS_TYPE_SORCERER)) + " "
        + IntToString(NWNX_Creature_GetCasterLevelOverride(c, CLASS_TYPE_WIZARD)) + " "
        + IntToString(NWNX_Creature_GetCasterLevelOverride(b, CLASS_TYPE_WIZARD)) + " "
        + IntToString(NWNX_Creature_GetCasterLevelModifier(a, CLASS_TYPE_WIZARD)));
    PLogI("acs.plain", NWNX_Creature_AddCastSpellActions(a, a, GetPosition(a), SPELL_MELFS_ACID_ARROW, 0, METAMAGIC_NONE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    PLogI("acs.mod", NWNX_Creature_AddCastSpellActions(b, b, GetPosition(b), SPELL_MELFS_ACID_ARROW, 0, METAMAGIC_NONE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    PLogI("acs.over", NWNX_Creature_AddCastSpellActions(c, c, GetPosition(c), SPELL_MELFS_ACID_ARROW, 0, METAMAGIC_NONE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    PLogI("acs.cheat", NWNX_Creature_AddCastSpellActions(d, d, GetPosition(d), SPELL_RESISTANCE, 255, METAMAGIC_NONE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    PLogI("acs.queued", NWNX_Creature_AddCastSpellActions(g, g, GetPosition(g), SPELL_DISPEL_MAGIC, 0));
    PLogI("acs.queuedactions", GetCurrentAction(g));
    PLogI("acs.badclass", NWNX_Creature_AddCastSpellActions(g, g, GetPosition(g), SPELL_DISPEL_MAGIC, 2));
    NWNX_Creature_DoItemCastSpell(e, e, GetLocation(e), SPELL_RESISTANCE, 7, 0.0, PROJECTILE_PATH_TYPE_DEFAULT, -1, OBJECT_INVALID, "p_cl");
    NWNX_Creature_DoItemCastSpell(f, f, GetLocation(f), SPELL_LIGHT, 7, 0.0);
    NWNX_Creature_SetLastItemCasterLevel(e, 9);
    PLogI("lastitemcl", NWNX_Creature_GetLastItemCasterLevel(e));
    PLogI("lastitemcl.default", NWNX_Creature_GetLastItemCasterLevel(a));
    PLog("acs.left", IntToString(GetSpellUsesLeft(a, CLASS_TYPE_WIZARD, SPELL_MELFS_ACID_ARROW)) + " " + IntToString(GetSpellUsesLeft(d, CLASS_TYPE_WIZARD, SPELL_RESISTANCE)));
    DelayCommand(1.0, PLogI("acs.queuedlater", GetCurrentAction(g)));
    DelayCommand(6.0, PDone());
}

void main()
{
    // A prepared caster.
    object oL = CreateObject(OBJECT_TYPE_CREATURE, "nw_lich001", PCentre());
    Slots("wiz", oL, CLASS_TYPE_WIZARD, 0, 9);
    PLogI("wiz.hasfireball", GetHasSpell(SPELL_FIREBALL, oL));
    NWNX_Creature_SetRemainingSpellSlots(oL, CLASS_TYPE_WIZARD, 3, 1);
    PLogI("wiz.set3", NWNX_Creature_GetRemainingSpellSlots(oL, CLASS_TYPE_WIZARD, 3));
    PLogI("wiz.hasfireball2", GetHasSpell(SPELL_FIREBALL, oL));
    NWNX_Creature_DecrementRemainingSpellSlots(oL, CLASS_TYPE_WIZARD, 2);
    PLogI("wiz.dec2", NWNX_Creature_GetRemainingSpellSlots(oL, CLASS_TYPE_WIZARD, 2));
    PLogI("wiz.notclass", NWNX_Creature_GetMaxSpellSlots(oL, CLASS_TYPE_CLERIC, 1));
    PLogI("wiz.notclassleft", NWNX_Creature_GetRemainingSpellSlots(oL, CLASS_TYPE_CLERIC, 1));

    // A spontaneous caster made by levelling a goblin.
    object oS = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(2.0));
    NWNX_Creature_SetRawAbilityScore(oS, ABILITY_CHARISMA, 16);
    NWNX_Creature_LevelUp(oS, CLASS_TYPE_SORCERER, 4);
    Slots("sor", oS, CLASS_TYPE_SORCERER, 1, 3);
    PLog("sor.known", IntToString(GetKnownSpellCount(oS, CLASS_TYPE_SORCERER, 0)) + " " + IntToString(GetKnownSpellCount(oS, CLASS_TYPE_SORCERER, 1)));
    int nSpell = GetKnownSpellId(oS, CLASS_TYPE_SORCERER, 1, 0);
    PLogI("sor.uses", GetSpellUsesLeft(oS, CLASS_TYPE_SORCERER, nSpell));
    NWNX_Creature_DecrementRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1);
    PLogI("sor.dec", NWNX_Creature_GetRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1));
    NWNX_Creature_SetRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1, 2);
    PLogI("sor.uses2", GetSpellUsesLeft(oS, CLASS_TYPE_SORCERER, nSpell));
    NWNX_Creature_DecrementRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1);
    PLogI("sor.usesdec", GetSpellUsesLeft(oS, CLASS_TYPE_SORCERER, nSpell));
    PLogI("sor.hasdec", GetHasSpell(nSpell, oS));
    NWNX_Creature_SetRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1, 0);
    NWNX_Creature_IncrementRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1);
    NWNX_Creature_IncrementRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1);
    NWNX_Creature_IncrementRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1);
    PLogI("sor.inc3", NWNX_Creature_GetRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1));
    NWNX_Creature_SetRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1, 0);
    PLogI("sor.set0", NWNX_Creature_GetRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1));
    PLogI("sor.has0", GetHasSpell(nSpell, oS));
    PLogI("sor.dec0", NWNX_Creature_GetRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1));
    NWNX_Creature_SetRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1, 40);
    PLogI("sor.set40", NWNX_Creature_GetRemainingSpellSlots(oS, CLASS_TYPE_SORCERER, 1));
    NWNX_Creature_ModifyNumberBonusSpells(oS, 1, 1, 2);
    Slots("sorbonus", oS, CLASS_TYPE_SORCERER, 1, 3);
    NWNX_Creature_ModifyNumberBonusSpells(oS, 1, 2, -1);
    Slots("sorbonus2", oS, CLASS_TYPE_SORCERER, 1, 3);
    NWNX_Creature_SetRawAbilityScore(oS, ABILITY_CHARISMA, 24);
    Slots("sorcha", oS, CLASS_TYPE_SORCERER, 1, 3);
    PLogI("sor.bonusbadpos", NWNX_Creature_GetNumberOfBonusSpells(oS, 2, 1));

    // Spell resistance.
    object oR = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(-2.0));
    PLogI("sr.default", GetSpellResistance(oR));
    NWNX_Creature_SetSpellResistance(oR, 15);
    PLogI("sr.set", GetSpellResistance(oR));
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSpellResistanceIncrease(20), oR, 1.0);
    PLogI("sr.fx", GetSpellResistance(oR));
    DelayCommand(2.0, PLogI("sr.after", GetSpellResistance(oR)));
    DelayCommand(2.5, Casts());
}
