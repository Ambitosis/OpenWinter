namespace OpenWinter.Game.Skills;

/// <summary>
/// Engine-coded numbers the skills package needs (from nwscript.nss). The engine keys skill behaviour
/// by skills.2da row, so modules that rename rows (e.g. a 5e-style skills.2da) still get the original
/// behaviour for row 9 = Open Lock and so on.
/// </summary>
public static class SkillIds
{
    // SKILL_* (skills.2da rows)
    public const int AnimalEmpathy = 0, Concentration = 1, DisableTrap = 2, Discipline = 3, Heal = 4, Hide = 5, Listen = 6,
        Lore = 7, MoveSilently = 8, OpenLock = 9, Parry = 10, Perform = 11, Persuade = 12, PickPocket = 13, Search = 14,
        SetTrap = 15, Spellcraft = 16, Spot = 17, Taunt = 18, UseMagicDevice = 19, Appraise = 20, Tumble = 21, CraftTrap = 22,
        Bluff = 23, Intimidate = 24;

    // SUBSKILL_*
    public const int SubSkillFlagTrap = 100, SubSkillRecoverTrap = 101, SubSkillExamineTrap = 102;

    // ACTION_* reported by GetCurrentAction while a skill action runs.
    public const int ActionDisableTrap = 8, ActionRecoverTrap = 9, ActionFlagTrap = 10, ActionExamineTrap = 11, ActionSetTrap = 12,
        ActionOpenLock = 13, ActionAnimalEmpathy = 16, ActionTaunt = 18, ActionHeal = 33, ActionPickPocket = 34, ActionInvalid = 65535;

    // ACTION_MODE_*
    public const int ModeDetect = 0, ModeStealth = 1, ModeParry = 2;

    // ITEM_PROPERTY_*
    public const int IpUseLimitationAlignmentGroup = 62, IpUseLimitationClass = 63, IpUseLimitationRacialType = 64,
        IpUseLimitationSpecificAlignment = 65, IpThievesTools = 55, IpTrap = 70, IpHealersKit = 80;

    // FEAT_*
    public const int FeatImprovedParry = 18, FeatTracklessStep = 201, FeatSkillMastery = 225, FeatKeenSense = 240,
        FeatBullheaded = 380, FeatHideInPlainSight = 433;

    // CLASS_TYPE_*
    public const int ClassRogue = 8;

    // RACIAL_TYPE_*
    public const int RaceAnimal = 8, RaceBeast = 9, RaceMagicalBeast = 19;

    // EFFECT_TYPE_*
    public const int EffectDominated = 26, EffectPoison = 31, EffectDisease = 32, EffectAcDecrease = 47, EffectSpellFailure = 82,
        EffectBlindness = 67;

    // INVENTORY_DISTURB_TYPE_*
    public const int DisturbStolen = 2;

    // CREATURE_SIZE_* (creaturesize.2da rows)
    public const int SizeTiny = 1, SizeSmall = 2, SizeMedium = 3, SizeLarge = 4, SizeHuge = 5;

    // Combat-log strings (dialog.tlk).
    public const uint StrSkillCheck = 10484, StrTake20 = 10485, StrOpposedCheck = 10473,
        StrStoleGold = 10421, StrStoleItem = 10422, StrAttemptingSteal = 10423, StrStoleItemFromYou = 10424, StrStoleMoneyFromYou = 10425,
        StrNoItemsOrGold = 10690, StrNotDiseasedOrPoisoned = 10688, StrInvalidHealTarget = 10689,
        StrNoStealthInCombat = 10719, StrInvalidTauntTarget = 7942,
        StrEffortless = 6416, StrEasy = 6417, StrChallenging = 6419, StrVeryDifficult = 6420, StrImpossible = 6422;
}
