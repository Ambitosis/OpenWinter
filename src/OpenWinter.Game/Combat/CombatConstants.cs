namespace OpenWinter.Game.Combat;

/// <summary>
/// nwscript.nss constants the combat rules use. They are fixed engine values (the rules run without a
/// script host); 2DA-driven numbers (bonuses, ranges, dice) come from ruleset.2da at run time instead.
/// </summary>
public static class CombatK
{
    // EFFECT_TYPE_*
    public const int EffectEntangle = 11, EffectDeaf = 13, EffectCharmed = 23, EffectConfused = 24, EffectFrightened = 25,
        EffectDominated = 26, EffectParalyze = 27, EffectDazed = 28, EffectStunned = 29, EffectSleep = 30, EffectSilence = 34,
        EffectTurned = 35, EffectHaste = 36, EffectSlow = 37, EffectAttackIncrease = 40, EffectAttackDecrease = 41,
        EffectDamageIncrease = 42, EffectDamageDecrease = 43, EffectInvisibility = 56, EffectImprovedInvisibility = 57,
        EffectDarkness = 58, EffectNegativeLevel = 61, EffectPolymorph = 62, EffectSanctuary = 63, EffectTrueSeeing = 64,
        EffectSeeInvisible = 65, EffectTimeStop = 66, EffectBlindness = 67, EffectUltravision = 70, EffectMissChance = 71,
        EffectConcealment = 72, EffectPetrify = 79, EffectCutsceneParalyze = 80, EffectEthereal = 81, EffectKnockdown = 98,
        EffectModifyAttacks = 99;

    // ATTACK_BONUS_* (EffectAttackIncrease modifier type)
    public const int AttackBonusMisc = 0, AttackBonusOnHand = 1, AttackBonusOffHand = 2;
    // MISS_CHANCE_TYPE_*
    public const int MissNormal = 0, MissVsRanged = 1, MissVsMelee = 2;
    // INVISIBILITY_TYPE_*
    public const int InvisibilityNormal = 1, InvisibilityDarkness = 2, InvisibilityImproved = 4;

    // DAMAGE_TYPE_*
    public const int DamageBludgeoning = 1, DamagePiercing = 2, DamageSlashing = 4, DamageMagical = 8, DamageAcid = 16,
        DamageCold = 32, DamageDivine = 64, DamageElectrical = 128, DamageFire = 256, DamageNegative = 512,
        DamagePositive = 1024, DamageSonic = 2048, DamageBaseWeapon = 4096;
    public const int PhysicalDamage = DamageBludgeoning | DamagePiercing | DamageSlashing;

    // SPECIAL_ATTACK_*
    public const int SpecialNone = 0, SpecialCalledShotLeg = 1, SpecialCalledShotArm = 2, SpecialSap = 3, SpecialDisarm = 4,
        SpecialImprovedDisarm = 5, SpecialKnockdown = 6, SpecialImprovedKnockdown = 7, SpecialStunningFist = 8,
        SpecialFlurry = 9, SpecialRapidShot = 10;

    // COMBAT_MODE_* (GetLastAttackMode)
    public const int CombatModeNone = 0, CombatModeParry = 1, CombatModePowerAttack = 2, CombatModeImprovedPowerAttack = 3,
        CombatModeFlurry = 4, CombatModeRapidShot = 5, CombatModeExpertise = 6, CombatModeImprovedExpertise = 7,
        CombatModeDefensiveCasting = 8, CombatModeDirtyFighting = 9, CombatModeDefensiveStance = 10;

    // ACTION_MODE_* (SetActionMode)
    public const int ModeDetect = 0, ModeStealth = 1, ModeParry = 2, ModePowerAttack = 3, ModeImprovedPowerAttack = 4,
        ModeCounterspell = 5, ModeFlurry = 6, ModeRapidShot = 7, ModeExpertise = 8, ModeImprovedExpertise = 9,
        ModeDefensiveCast = 10, ModeDirtyFighting = 11;

    // RACIAL_TYPE_*
    public const int RaceConstruct = 10, RaceGoblinoid = 12, RaceReptilian = 15, RaceOrc = 14, RaceGiant = 18,
        RaceUndead = 24, RaceOoze = 29;

    // SKILL_*
    public const int SkillDiscipline = 3, SkillParry = 10;

    // BASE_ITEM_* the rules treat specially
    public const int BaseGloves = 36, BaseKama = 40, BaseRapier = 51, BaseBracer = 78;

    // baseitems.2da WeaponWield values
    public const int WieldTwoHanded = 4, WieldBow = 5, WieldCrossbow = 6, WieldDouble = 8, WieldCreature = 9, WieldSling = 10, WieldThrown = 11;

    // ITEM_PROPERTY_*
    public const int IpEnhancement = 6, IpEnhancementVsAlignGroup = 7, IpEnhancementVsRace = 8, IpEnhancementVsAlign = 9,
        IpDecreasedEnhancement = 10, IpCastSpell = 15, IpDamageBonus = 16, IpDamageVsAlignGroup = 17, IpDamageVsRace = 18,
        IpDamageVsAlign = 19, IpDecreasedDamage = 21, IpHaste = 35, IpExtraMeleeDamageType = 33, IpExtraRangedDamageType = 34, IpKeen = 43,
        IpMighty = 45, IpOnHit = 48, IpAttackBonus = 56, IpAttackVsAlignGroup = 57, IpAttackVsRace = 58, IpAttackVsAlign = 59,
        IpDecreasedAttack = 60, IpUnlimitedAmmo = 61, IpMassiveCriticals = 74, IpMonsterDamage = 77, IpOnHitCastSpell = 82;

    // FEAT_*
    public const int FeatAmbidexterity = 1, FeatCalledShot = 5, FeatCleave = 6, FeatDeflectArrows = 8, FeatDisarm = 9,
        FeatDodge = 10, FeatImprovedDisarm = 16, FeatImprovedKnockdown = 17, FeatImprovedParry = 18,
        FeatImprovedTwoWeaponFighting = 20, FeatImprovedPowerAttack = 19, FeatPowerAttack = 28, FeatExpertise = 389,
        FeatImprovedExpertise = 390, FeatKnockdown = 23, FeatMobility = 26, FeatPointBlankShot = 27, FeatRapidShot = 30,
        FeatSap = 31, FeatStunningFist = 39, FeatTwoWeaponFighting = 41, FeatWeaponFinesse = 42,
        FeatImprovedCriticalUnarmed = 62, FeatWeaponFocusUnarmed = 100, FeatWeaponSpecializationUnarmed = 138,
        FeatUncannyDodge1 = 195, FeatFlurryOfBlows = 204, FeatKiStrike = 213, FeatKiStrike2 = 343, FeatKiStrike3 = 344,
        FeatOpportunist = 224, FeatBattleTrainingOrcs = 231, FeatBattleTrainingGoblins = 232, FeatBattleTrainingGiants = 233,
        FeatBattleTrainingReptilians = 242, FeatGoodAim = 250, FeatUncannyDodge2 = 251, FeatUncannyDodge6 = 255,
        FeatQuiveringPalm = 296, FeatSmiteEvil = 301, FeatGreatCleave = 391, FeatSpringAttack = 392,
        FeatBlindFight = 408, FeatCircleKick = 409, FeatRapidReload = 411, FeatZenArchery = 412, FeatDirtyFighting = 425,
        FeatHideInPlainSight = 433, FeatSmiteGood = 472, FeatEpicProwess = 584, FeatEpicWeaponFocusUnarmed = 630,
        FeatEpicWeaponSpecializationUnarmed = 668, FeatEpicKiStrike4 = 697, FeatEpicKiStrike5 = 698,
        FeatEpicOverwhelmingCriticalUnarmed = 720, FeatEpicDevastatingCriticalUnarmed = 506,
        FeatEpicSelfConcealment10 = 748, FeatEpicBaneOfEnemies = 855, FeatEpicDodge = 856, FeatDefensiveRoll = 223, FeatWhirlwindAttack = 867,
        FeatImprovedWhirlwind = 868, FeatIncreaseMultiplier = 883, FeatSuperiorWeaponFocus = 884, FeatKiCritical = 885,
        FeatEpicSuperiorWeaponFocus = 1071, FeatImprovedUnarmedStrike = 21;

    /// <summary>Favoured enemy feats (FEAT_FAVORED_ENEMY_*) and the RACIAL_TYPE_* each one is for.</summary>
    public static readonly (int Feat, int Race)[] FavoredEnemies =
    {
        (261, 0), (262, 1), (263, 2), (264, 3), (265, 4), (266, 5), (267, 6), (268, 7), (269, 8), (270, 9), (271, 10),
        (272, 11), (273, 12), (274, 13), (275, 14), (276, 15), (277, 16), (278, 17), (279, 18), (280, 19), (281, 20),
        (284, 23), (285, 24), (286, 25),
    };

    static int[] Run(params (int From, int To)[] runs) => runs.SelectMany(r => Enumerable.Range(r.From, r.To - r.From + 1)).ToArray();

    /// <summary>Sneak attack feat chains in order: owning the n-th feat of a chain means n dice (the best feat of each chain counts).</summary>
    public static readonly int[][] SneakAttackChains =
    {
        Run((221, 221), (345, 353), (1032, 1041)),   // rogue Sneak Attack 1..20
        Run((460, 462), (1007, 1018)),               // blackguard sneak attack 1d6..15d6
        Run((834, 843)),                             // epic Improved Sneak Attack 1..10
    };

    /// <summary>Assassin Death Attack 1..20 (dice against targets denied their Dexterity bonus).</summary>
    public static readonly int[] DeathAttackChain = Run((455, 459), (1004, 1006), (1019, 1030));

    /// <summary>Arcane archer Enchant Arrow feats (+1..+20).</summary>
    public static readonly (int From, int To, int FirstBonus)[] EnchantArrowChains = { (445, 449, 1), (1045, 1059, 6) };

    /// <summary>Epic Great Smiting feats 1..10.</summary>
    public const int FeatEpicGreatSmiting1 = 824, FeatEpicGreatSmiting10 = 833;
    /// <summary>Epic Improved Stunning Fist 1..10.</summary>
    public const int FeatEpicImprovedStunningFist1 = 844, FeatEpicImprovedStunningFist10 = 853;
    /// <summary>Epic Self Concealment 10..50 (feats 748..752).</summary>
    public const int FeatEpicSelfConcealment50 = 752;
}
