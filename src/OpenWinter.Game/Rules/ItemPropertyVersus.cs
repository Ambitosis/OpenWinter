using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Rules;

/// <summary>
/// The "versus" item properties (AC, enhancement, attack and damage bonuses against an alignment
/// group, a racial type, a specific alignment or a damage type) and whether one applies against an
/// object. Subtypes: iprp_aligngrp.2da (0 all, 1 neutral, 2 lawful, 3 chaotic, 4 good, 5 evil),
/// racialtypes.2da rows, iprp_alignment.2da (0 LG .. 8 CE) and iprp_damagetype.2da.
/// </summary>
public static class ItemPropertyVersus
{
    // ITEM_PROPERTY_* rows of itempropdef.2da by what they are restricted to.
    public const int AcVsAlignGroup = 2, AcVsDamageType = 3, AcVsRace = 4, AcVsAlign = 5,
        EnhancementVsAlignGroup = 7, EnhancementVsRace = 8, EnhancementVsAlign = 9,
        DamageVsAlignGroup = 17, DamageVsRace = 18, DamageVsAlign = 19,
        AttackVsAlignGroup = 57, AttackVsRace = 58, AttackVsAlign = 59;

    /// <summary>Whether a property applies against <paramref name="versus"/> (always true for unrestricted properties).</summary>
    public static bool Applies(World w, ItemProperty ip, GameObject? versus, int damageType = 0) => ip.Type switch
    {
        AcVsAlignGroup or EnhancementVsAlignGroup or DamageVsAlignGroup or AttackVsAlignGroup => AlignGroup(ip.SubType, versus),
        AcVsRace or EnhancementVsRace or DamageVsRace or AttackVsRace => versus is Creature c && c.Race == ip.SubType,
        AcVsAlign or EnhancementVsAlign or DamageVsAlign or AttackVsAlign => SpecificAlignment(ip.SubType, versus),
        AcVsDamageType => (ItemPropertyRules.DamageTypeBits(ip.SubType) & damageType) != 0,
        _ => true,
    };

    /// <summary>iprp_aligngrp row against a creature: 0 all, 1 neutral (either axis), 2 lawful, 3 chaotic, 4 good, 5 evil.</summary>
    public static bool AlignGroup(int group, GameObject? versus)
    {
        if (versus is not Creature c) return false;
        int lc = CreatureRules.LawChaos(c.LawfulChaotic), ge = CreatureRules.GoodEvil(c.GoodEvil);
        return group switch
        {
            0 => true,
            1 => lc == NwRules.AlignNeutral || ge == NwRules.AlignNeutral,
            2 => lc == NwRules.AlignLawful,
            3 => lc == NwRules.AlignChaotic,
            4 => ge == NwRules.AlignGood,
            5 => ge == NwRules.AlignEvil,
            _ => false,
        };
    }

    /// <summary>iprp_alignment row (0 LG, 1 LN, 2 LE, 3 NG, 4 TN, 5 NE, 6 CG, 7 CN, 8 CE) against a creature.</summary>
    public static bool SpecificAlignment(int row, GameObject? versus)
    {
        if (versus is not Creature c || row < 0 || row > 8) return false;
        int wantLc = (row / 3) switch { 0 => NwRules.AlignLawful, 1 => NwRules.AlignNeutral, _ => NwRules.AlignChaotic };
        int wantGe = (row % 3) switch { 0 => NwRules.AlignGood, 1 => NwRules.AlignNeutral, _ => NwRules.AlignEvil };
        return CreatureRules.LawChaos(c.LawfulChaotic) == wantLc && CreatureRules.GoodEvil(c.GoodEvil) == wantGe;
    }
}
