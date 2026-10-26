// NWNX_Feat: what feat modifiers give a creature that has the feat (its effects and the rules they change): made
// with the feat, gained with NWNX_Creature_AddFeat, lost with RemoveFeat, not given to creatures that already
// existed when the modifiers were set.
#include "parity_inc"
#include "nwnx_feat"
#include "nwnx_effect"
#include "nwnx_creature"

string O(object o) { return o == OBJECT_INVALID ? "inv" : GetIsObjectValid(o) ? GetTag(o) : "other"; }

void DumpTrue(string k, object o)
{
    int n = NWNX_Effect_GetTrueEffectCount(o);
    int i;
    for (i = 0; i < n; i++)
    {
        struct NWNX_EffectUnpacked u = NWNX_Effect_GetTrueEffect(o, i);
        // A regeneration's next tick is a calendar time: only its amount and interval are compared.
        string sInts = IntToString(u.nParam0) + "," + IntToString(u.nParam1);
        if (u.nType != 7) sInts += "," + IntToString(u.nParam2) + "," + IntToString(u.nParam3) + "," + IntToString(u.nParam4) + "," + IntToString(u.nParam5);
        PLog(k + "." + IntToString(i), IntToString(u.nType) + "/" + IntToString(u.nSubType) + ":" + sInts
            + ":" + O(u.oCreator) + ":" + IntToString(u.nSpellId) + ":" + u.sTag + ":" + IntToString(u.bExpose) + "/" + IntToString(u.bShowIcon) + ":" + FloatToString(u.fDuration, 0, 1));
    }
    PLogI(k + ".count", n);
}

void Stats(string k, object o)
{
    PLogI(k + ".ac", GetAC(o));
    PLog(k + ".saves", IntToString(GetFortitudeSavingThrow(o)) + "/" + IntToString(GetReflexSavingThrow(o)) + "/" + IntToString(GetWillSavingThrow(o)));
    PLog(k + ".immune", IntToString(GetIsImmune(o, IMMUNITY_TYPE_FEAR)) + "/" + IntToString(GetIsImmune(o, IMMUNITY_TYPE_POISON)));
    PLogI(k + ".sr", GetSpellResistance(o));
    PLogI(k + ".str", GetAbilityScore(o, ABILITY_STRENGTH));
    effect e = GetFirstEffect(o);
    string s = "";
    while (GetIsEffectValid(e)) { s += " " + IntToString(GetEffectType(e)); e = GetNextEffect(o); }
    PLog(k + ".scriptfx", s);
}

void Later()
{
    DumpTrue("later.has", GetObjectByTag("FT_HAS"));
    DumpTrue("later.added", GetObjectByTag("FT_ADDED"));
    Stats("later.added", GetObjectByTag("FT_ADDED"));
    PDone();
}

void main()
{
    // Alertness (0): one of each kind of modifier that works through an effect.
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_AB, 2);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_AC, 3);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_ABILITY, ABILITY_STRENGTH, 4);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_SAVE, SAVING_THROW_FORT, 2);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_SAVEVSTYPE, SAVING_THROW_WILL, SAVING_THROW_TYPE_FEAR, 3);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_IMMUNITY, IMMUNITY_TYPE_POISON);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_DMGRESIST, DAMAGE_TYPE_FIRE, 10);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_DMGIMMUNITY, DAMAGE_TYPE_COLD, 50);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_DMGREDUCTION, 5, 2);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_CONCEALMENT, 20);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_MOVEMENTSPEED, 50);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_REGENERATION, 2, 6);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_SPELLIMMUNITY, SPELL_MAGIC_MISSILE);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_SRCHARGEN, 12);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_ABVSRACE, RACIAL_TYPE_ELF, 3);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_ACVSRACE, RACIAL_TYPE_ELF, 4);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_SAVEVSRACE, SAVING_THROW_ALL, RACIAL_TYPE_UNDEAD, 2);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_SEEINVISIBLE);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_ULTRAVISION);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_TRUESEEING);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_VISUALEFFECT, VFX_DUR_GLOW_BLUE);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_ARCANESPELLFAILURE, -10);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_DAMAGE, DAMAGE_TYPE_FIRE, DAMAGE_BONUS_2);
    NWNX_Feat_SetFeatModifier(FEAT_ALERTNESS, NWNX_FEAT_MODIFIER_SAVEVSTYPERACE, SAVING_THROW_REFLEX, SAVING_THROW_TYPE_TRAP, RACIAL_TYPE_ELF, 1);
    // Toughness: penalties and repeats.
    NWNX_Feat_SetFeatModifier(FEAT_TOUGHNESS, NWNX_FEAT_MODIFIER_AB, -1);
    NWNX_Feat_SetFeatModifier(FEAT_TOUGHNESS, NWNX_FEAT_MODIFIER_AC, -2);
    NWNX_Feat_SetFeatModifier(FEAT_TOUGHNESS, NWNX_FEAT_MODIFIER_SAVE, SAVING_THROW_WILL, -1);
    NWNX_Feat_SetFeatModifier(FEAT_TOUGHNESS, NWNX_FEAT_MODIFIER_ABILITY, ABILITY_DEXTERITY, -2);
    NWNX_Feat_SetFeatModifier(FEAT_TOUGHNESS, NWNX_FEAT_MODIFIER_DMGRESIST, DAMAGE_TYPE_ACID, 5);
    NWNX_Feat_SetFeatModifier(FEAT_TOUGHNESS, NWNX_FEAT_MODIFIER_DMGRESIST, DAMAGE_TYPE_ACID, 5);
    NWNX_Feat_SetFeatModifier(FEAT_TOUGHNESS, NWNX_FEAT_MODIFIER_MOVEMENTSPEED, -20);
    // Dodge: haste (the original adds its own parts to a haste, so only the armour class it gives is compared).
    NWNX_Feat_SetFeatModifier(FEAT_DODGE, NWNX_FEAT_MODIFIER_HASTE);

    object oPlaced = GetObjectByTag("FT_PLACED");
    PLogI("placed.feat", GetHasFeat(FEAT_ALERTNESS, oPlaced));
    DumpTrue("placed", oPlaced);

    object oA = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(), FALSE, "FT_HAS");
    PLogI("has.feat", GetHasFeat(FEAT_ALERTNESS, oA));
    DumpTrue("has", oA);
    Stats("has", oA);

    object oPlain = CreateObject(OBJECT_TYPE_CREATURE, "nw_badger", PCentre(-4.0), FALSE, "FT_ADDED");
    PLogI("plain.feat", GetHasFeat(FEAT_ALERTNESS, oPlain));
    Stats("plain", oPlain);
    NWNX_Creature_AddFeat(oPlain, FEAT_ALERTNESS);
    PLogI("added.feat", GetHasFeat(FEAT_ALERTNESS, oPlain));
    DumpTrue("added", oPlain);
    Stats("added", oPlain);

    object oTough = CreateObject(OBJECT_TYPE_CREATURE, "nw_badger", PCentre(-6.0), FALSE, "FT_TOUGH");
    NWNX_Creature_AddFeat(oTough, FEAT_TOUGHNESS);
    DumpTrue("tough", oTough);
    Stats("tough", oTough);
    PLogF("tough.speed", GetMovementRate(oTough) * 1.0);

    object oHaste = CreateObject(OBJECT_TYPE_CREATURE, "nw_badger", PCentre(-8.0), FALSE, "FT_HASTE");
    int nAC = GetAC(oHaste);
    NWNX_Creature_AddFeat(oHaste, FEAT_DODGE);
    PLogI("haste.ac", GetAC(oHaste) - nAC);
    NWNX_Creature_RemoveFeat(oHaste, FEAT_DODGE);
    PLogI("haste.removedac", GetAC(oHaste) - nAC);

    object oRem = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(4.0), FALSE, "FT_REMOVED");
    NWNX_Creature_RemoveFeat(oRem, FEAT_ALERTNESS);
    PLogI("removed.feat", GetHasFeat(FEAT_ALERTNESS, oRem));
    DumpTrue("removed", oRem);
    Stats("removed", oRem);
    // What the original adds to vision effects.
    object oV1 = CreateObject(OBJECT_TYPE_CREATURE, "nw_badger", PCentre(8.0), FALSE, "FT_V1");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectUltravision(), oV1);
    DumpTrue("ultra", oV1);
    object oV2 = CreateObject(OBJECT_TYPE_CREATURE, "nw_badger", PCentre(10.0), FALSE, "FT_V2");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTrueSeeing(), oV2);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSeeInvisible(), oV2);
    DumpTrue("seeing", oV2);
    DelayCommand(1.0, Later());
}
