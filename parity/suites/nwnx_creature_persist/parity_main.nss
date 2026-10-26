// NWNX_Creature settings with and without bPersist: whether they travel with the creature's saved form (a copy
// is made from it), and where in that form they are kept.
#include "parity_inc"
#include "nwnx_creature"

void Set(object o, int bPersist)
{
    NWNX_Creature_SetCriticalMultiplierModifier(o, 2, 0, bPersist);
    NWNX_Creature_SetCriticalRangeOverride(o, 15, 1, bPersist);
    NWNX_Creature_SetCasterLevelModifier(o, CLASS_TYPE_FIGHTER, 3, bPersist);
    NWNX_Creature_SetCasterLevelOverride(o, CLASS_TYPE_WIZARD, 7, bPersist);
    NWNX_Creature_SetMaximumBonusAttacks(o, 8, bPersist);
    NWNX_Creature_SetInitiativeModifier(o, 4, bPersist);
    NWNX_Creature_SetBypassEffectImmunity(o, IMMUNITY_TYPE_STUN, 60, bPersist);
    NWNX_Creature_OverrideRangedProjectileVFX(o, NWNX_CREATURE_PROJECTILE_VFX_FIRE, bPersist);
}

void Read(string k, object o)
{
    PLog(k, IntToString(NWNX_Creature_GetCriticalMultiplierModifier(o)) + " " + IntToString(NWNX_Creature_GetCriticalRangeOverride(o, 1))
        + " " + IntToString(NWNX_Creature_GetCasterLevelModifier(o, CLASS_TYPE_FIGHTER)) + " " + IntToString(NWNX_Creature_GetCasterLevelOverride(o, CLASS_TYPE_WIZARD))
        + " " + IntToString(NWNX_Creature_GetMaximumBonusAttacks(o)) + " " + IntToString(NWNX_Creature_GetInitiativeModifier(o))
        + " " + IntToString(NWNX_Creature_GetBypassEffectImmunity(o, IMMUNITY_TYPE_STUN)));
}

// Where in the object's JSON the NWNX data sits, if anywhere.
void Where(string k, object o)
{
    string s = JsonDump(ObjectToJson(o));
    string m = "\"NWNX_POS\":{\"type\":\"cexostring\",\"value\":\"";
    int n = 0, i = FindSubString(s, m);
    while (i >= 0)
    {
        string v = GetSubString(s, i + GetStringLength(m), 3000);
        int e = FindSubString(v, "\"}");
        if (e > 0) { n++; WriteTimestampedLogEntry("RAW|" + k + IntToString(n) + "|" + GetStringLeft(v, e)); }
        i = FindSubString(s, m, i + 1);
    }
    PLogI(k + ".filled", n);
}

void main()
{
    object oP = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(), FALSE, "persist");
    object oN = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(2.0), FALSE, "plain");
    Set(oP, TRUE);
    Set(oN, FALSE);
    Read("persist.self", oP);
    Read("plain.self", oN);
    Read("persist.copy", CopyObject(oP, PCentre(0.0, 3.0)));
    Read("plain.copy", CopyObject(oN, PCentre(2.0, 3.0)));
    Read("persist.json", JsonToObject(ObjectToJson(oP), PCentre(0.0, 6.0)));
    Where("persist", oP);
    Where("plain", oN);

    // The other keys: incoming and every-type bypass, base items, the other critical settings; cleared overrides;
    // a value set again without bPersist.
    object oK = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(4.0), FALSE, "keys");
    NWNX_Creature_SetBypassEffectImmunity(oK, -IMMUNITY_TYPE_STUN, -40, TRUE);
    NWNX_Creature_SetBypassEffectImmunity(oK, 255, 30, TRUE);
    NWNX_Creature_SetCriticalRangeModifier(oK, -2, 2, TRUE, BASE_ITEM_DAGGER);
    NWNX_Creature_SetCriticalMultiplierOverride(oK, 4, 1, TRUE, BASE_ITEM_GLOVES);
    NWNX_Creature_SetCriticalMultiplierModifier(oK, 1, 0, TRUE);
    NWNX_Creature_SetCriticalRangeOverride(oK, 12, 0, TRUE);
    NWNX_Creature_SetCriticalRangeOverride(oK, -1, 0, TRUE);
    NWNX_Creature_SetCasterLevelOverride(oK, CLASS_TYPE_WIZARD, 7, TRUE);
    NWNX_Creature_SetCasterLevelOverride(oK, CLASS_TYPE_WIZARD, -1, TRUE);
    NWNX_Creature_SetInitiativeModifier(oK, 4, TRUE);
    NWNX_Creature_SetInitiativeModifier(oK, 6, FALSE);
    NWNX_Creature_SetMaximumBonusAttacks(oK, 8, TRUE);
    NWNX_Creature_SetMaximumBonusAttacks(oK, -1, TRUE);
    NWNX_Creature_OverrideRangedProjectileVFX(oK, NWNX_CREATURE_PROJECTILE_VFX_ACID, TRUE);
    NWNX_Creature_OverrideRangedProjectileVFX(oK, -1, TRUE);
    Where("keys", oK);
    object oKc = CopyObject(oK, PCentre(4.0, 3.0));
    PLog("keys.copy", IntToString(NWNX_Creature_GetInitiativeModifier(oKc)) + " " + IntToString(NWNX_Creature_GetBypassEffectImmunity(oKc, -IMMUNITY_TYPE_STUN))
        + " " + IntToString(NWNX_Creature_GetBypassEffectImmunity(oKc, 255)) + " " + IntToString(NWNX_Creature_GetCriticalRangeModifier(oKc, 2, BASE_ITEM_DAGGER))
        + " " + IntToString(NWNX_Creature_GetCriticalMultiplierOverride(oKc, 1, BASE_ITEM_GLOVES)) + " " + IntToString(NWNX_Creature_GetCriticalRangeOverride(oKc))
        + " " + IntToString(NWNX_Creature_GetCasterLevelOverride(oKc, CLASS_TYPE_WIZARD)) + " " + IntToString(NWNX_Creature_GetMaximumBonusAttacks(oKc)));
    PDone();
}
