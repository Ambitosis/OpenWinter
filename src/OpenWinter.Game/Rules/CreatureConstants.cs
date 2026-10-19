namespace OpenWinter.Game.Rules;

/// <summary>
/// nwscript.nss constants the creature rules need. They are fixed engine values (the rules layer
/// runs without a script host, so it cannot read them from the definitions).
/// </summary>
public static class NwRules
{
    public const int AbilityStr = 0, AbilityDex = 1, AbilityCon = 2, AbilityInt = 3, AbilityWis = 4, AbilityCha = 5;

    // EFFECT_TYPE_*
    public const int EffectTemporaryHitpoints = 9, EffectImmunity = 15, EffectCurse = 33,
        EffectAbilityIncrease = 38, EffectAbilityDecrease = 39, EffectAttackIncrease = 40, EffectAttackDecrease = 41,
        EffectAcIncrease = 46, EffectAcDecrease = 47, EffectSavingThrowIncrease = 50, EffectSavingThrowDecrease = 51,
        EffectSpellResistanceIncrease = 52, EffectSpellResistanceDecrease = 53, EffectSkillIncrease = 54, EffectSkillDecrease = 55,
        EffectNegativeLevel = 61, EffectPolymorph = 62, EffectBonusFeat = 88,
        EffectEntangle = 11, EffectArcaneSpellFailure = 18, EffectFrightened = 25, EffectParalyze = 27, EffectStunned = 29,
        EffectSleep = 30, EffectTurned = 35, EffectHaste = 36, EffectSlow = 37, EffectDarkness = 58, EffectBlindness = 67,
        EffectPetrify = 79, EffectCutsceneParalyze = 80, EffectCutsceneImmobilize = 84,
        EffectMovementSpeedIncrease = 48, EffectMovementSpeedDecrease = 49;

    // AC_* modifier types
    public const int AcDodge = 0, AcNatural = 1, AcArmour = 2, AcShield = 3, AcDeflection = 4, AcVsDamageTypeAll = 4103;

    // SAVING_THROW_*
    public const int SaveAll = 0, SaveFort = 1, SaveReflex = 2, SaveWill = 3;
    // SAVING_THROW_TYPE_*
    public const int SaveTypeAll = 0, SaveTypeMind = 1, SaveTypePoison = 2, SaveTypeDisease = 3, SaveTypeFear = 4, SaveTypeSonic = 5,
        SaveTypeAcid = 6, SaveTypeFire = 7, SaveTypeElectricity = 8, SaveTypePositive = 9, SaveTypeNegative = 10, SaveTypeDeath = 11,
        SaveTypeCold = 12, SaveTypeDivine = 13, SaveTypeTrap = 14, SaveTypeSpell = 15, SaveTypeGood = 16, SaveTypeEvil = 17,
        SaveTypeLaw = 18, SaveTypeChaos = 19, SaveTypeParalysis = 20;

    // IMMUNITY_TYPE_*
    public const int ImmunityNone = 0, ImmunityMind = 1, ImmunityPoison = 2, ImmunityDisease = 3, ImmunityFear = 4, ImmunityTrap = 5,
        ImmunityParalysis = 6, ImmunityEntangle = 10, ImmunitySlow = 9, ImmunitySleep = 13, ImmunityAbilityDecrease = 19,
        ImmunityMovementSpeedDecrease = 24, ImmunityKnockdown = 28, ImmunityNegativeLevel = 29, ImmunitySneakAttack = 30,
        ImmunityCriticalHit = 31, ImmunityDeath = 32;

    // ITEM_PROPERTY_*
    public const int IpAbilityBonus = 0, IpAcBonus = 1, IpAcBonusVsAlignmentGroup = 2, IpAcBonusVsDamageType = 3,
        IpAcBonusVsRacialGroup = 4, IpAcBonusVsSpecificAlignment = 5, IpBonusFeat = 12, IpBonusSpellSlot = 13, IpDecreasedAbility = 27, IpDecreasedAc = 28,
        IpDecreasedSkill = 29, IpImmunityMisc = 37, IpSpellResistance = 39, IpSaveBonus = 40, IpSaveBonusSpecific = 41,
        IpMindBlank = 46, IpHaste = 35, IpDecreasedSaves = 49, IpDecreasedSavesSpecific = 50, IpSkillBonus = 52, IpFreedomOfMovement = 75,
        IpWeightIncrease = 81, IpBaseItemWeightReduction = 11, IpArcaneSpellFailure = 84;

    // ALIGNMENT_*
    public const int AlignAll = 0, AlignNeutral = 1, AlignLawful = 2, AlignChaotic = 3, AlignGood = 4, AlignEvil = 5;

    // TALENT_TYPE_*
    public const int TalentSpell = 0, TalentFeat = 1, TalentSkill = 2;

    // ASSOCIATE_TYPE_*
    public const int AssociateCompanion = 2, AssociateFamiliar = 3;

    // CLASS_TYPE_* the engine treats specially.
    public const int ClassBarbarian = 0, ClassBard = 1, ClassCleric = 2, ClassDruid = 3, ClassFighter = 4, ClassMonk = 5, ClassPaladin = 6,
        ClassRanger = 7, ClassRogue = 8, ClassSorcerer = 9, ClassWizard = 10, ClassShadowdancer = 27, ClassBlackguard = 31,
        ClassPaleMaster = 34, ClassDwarvenDefender = 36, ClassDragonDisciple = 37, ClassInvalid = 255;

    public const int RacialTypeInvalid = 28, RacialTypeUndead = 24;
    public const int SkillTumble = 21, SkillAll = 255;

    // FEAT_* with engine-coded rules.
    public const int FeatAlertness = 0, FeatDodge = 10, FeatExtraTurning = 13, FeatGreatFortitude = 14, FeatIronWill = 22,
        FeatLightningReflexes = 24, FeatStunningFist = 39, FeatToughness = 40, FeatAnimalCompanion = 199, FeatVenomImmunity = 203,
        FeatStillMind = 208, FeatPurityOfBody = 209, FeatDiamondBody = 214, FeatDiamondSoul = 215, FeatDivineGrace = 217,
        FeatDivineHealth = 219, FeatHardinessPoisons = 229, FeatHardinessSpells = 230, FeatSkillAffinityLore = 234,
        FeatImmunityToSleep = 235, FeatHardinessEnchantments = 236, FeatSkillAffinityListen = 237, FeatSkillAffinitySearch = 238,
        FeatSkillAffinitySpot = 239, FeatSkillAffinityConcentration = 243, FeatPartialAffinityListen = 244,
        FeatPartialAffinitySearch = 245, FeatPartialAffinitySpot = 246, FeatSkillAffinityMoveSilently = 247, FeatLucky = 248,
        FeatFearless = 249, FeatBardSongs = 257, FeatMonkAcBonus = 260, FeatTurnUndead = 294, FeatAuraOfCourage = 300,
        FeatSmiteEvil = 301, FeatSummonFamiliar = 303, FeatArtist = 378, FeatBlooded = 379, FeatBullheaded = 380,
        FeatCourtlyMagocracy = 381, FeatLuckOfHeroes = 382, FeatResistPoison = 383, FeatSilverPalm = 384, FeatSnakeblood = 386,
        FeatStealthy = 387, FeatThug = 402, FeatExtraStunning = 410, FeatExtraMusic = 423, FeatResistDisease = 426,
        FeatSmiteGood = 472, FeatDarkBlessing = 473, FeatEpicArmorSkin = 490, FeatEpicFortitude = 583, FeatEpicReflexes = 585,
        FeatEpicWill = 695, FeatEpicImprovedSr1 = 699, FeatEpicImprovedSr10 = 708, FeatEpicToughness1 = 754, FeatEpicToughness10 = 763,
        FeatStonecunning = 227, FeatStrongSoul = 388, FeatDeneirsEye = 437, FeatShouDodge2 = 489, FeatShouDodge3 = 1031,
        FeatBoneSkin2 = 886, FeatBoneSkin4 = 887, FeatBoneSkin6 = 888, FeatDragonArmor = 961;

    // METAMAGIC_*
    public const int MetaNone = 0, MetaEmpower = 1, MetaExtend = 2, MetaMaximize = 4, MetaQuicken = 8, MetaSilent = 16, MetaStill = 32;

    /// <summary>Spell level adjustment of a metamagic feat (spontaneous casters spend a higher slot).</summary>
    public static int MetaMagicLevels(int meta) => meta switch
    {
        MetaEmpower => 2,
        MetaExtend => 1,
        MetaMaximize => 3,
        MetaQuicken => 4,
        MetaSilent => 1,
        MetaStill => 1,
        _ => 0,
    };

    /// <summary>ABILITY_* index from a 2DA ability label (STR, DEX, ...), or -1.</summary>
    public static int AbilityFromLabel(string label) => label.ToUpperInvariant() switch
    {
        "STR" => AbilityStr,
        "DEX" => AbilityDex,
        "CON" => AbilityCon,
        "INT" => AbilityInt,
        "WIS" => AbilityWis,
        "CHA" => AbilityCha,
        _ => -1,
    };
}
