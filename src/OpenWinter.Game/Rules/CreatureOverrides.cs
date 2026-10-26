using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Per-creature adjustments to the rules that scripts can set (NWNX_Creature): movement rate factor and caps,
/// body sizes, critical hit settings, caster levels, bonus spells, bonus attack limit, initiative, immunity bypass
/// and a few more. Null fields mean "as the rules have it". Rules code reads them through
/// <see cref="Creature.Overrides"/>, which stays null on creatures nothing has adjusted.
/// </summary>
public sealed class CreatureOverrides
{
    /// <summary>A movement rate factor set directly; it stands until the creature's speed effects change.</summary>
    public float? MovementFactor;
    /// <summary>The speed effects (by identity, and how many) the factor was set against.</summary>
    public int MovementFactorEffects, MovementFactorEffectCount;
    /// <summary>When a speed effect was seen gone (the set factor lasts out that moment), or null.</summary>
    public double? MovementFactorRemovedAt;
    /// <summary>Upper limit of the movement rate factor (0: the ruleset's MOVEMENT_SPEED_BONUS_DEFAULT_CAP).</summary>
    public float MovementFactorCap;
    /// <summary>Highest walking speed in m/s, or null for none.</summary>
    public float? WalkRateCap;

    /// <summary>appearance.2da PERSPACE, CREPERSPACE, HEIGHT, HITDIST and PREFATCKDIST set for this creature.</summary>
    public float? PersonalSpace, CreaturePersonalSpace, Height, HitDistance, PreferredAttackDistance;

    /// <summary>Bonus attacks EffectModifyAttacks may give (the original's limit is 5).</summary>
    public int? MaxBonusAttacks;
    /// <summary>Base attack bonus that replaces the class tables' (0 or less: none).</summary>
    public int BaseAttackBonus;

    /// <summary>Critical hit adjustments by (hand: 0 all, 1 main, 2 off; base item, -1 for all).</summary>
    public readonly Dictionary<(int Hand, int BaseItem), int> CritMultiplierModifier = new(), CritMultiplierOverride = new(),
        CritRangeModifier = new(), CritRangeOverride = new();

    /// <summary>Caster level adjustments by class.</summary>
    public readonly Dictionary<int, int> CasterLevelModifier = new(), CasterLevelOverride = new();
    /// <summary>Extra spell slots by (class position, spell level).</summary>
    public readonly Dictionary<(int Position, int Level), int> BonusSpells = new();

    /// <summary>Initiative modifier that replaces any other custom one.</summary>
    public int InitiativeModifier;
    /// <summary>
    /// Chances to bypass an immunity: IMMUNITY_TYPE_* for this creature's effects on others, its negative for others'
    /// effects on this creature; 255 stands for every type. A positive chance makes the target not immune, a negative
    /// one immune.
    /// </summary>
    public readonly Dictionary<int, int> BypassImmunity = new();

    /// <summary>A spell resistance set directly, standing until its spell resistance effects change.</summary>
    public int? SpellResistance;
    public int SpellResistanceEffects;

    /// <summary>The creature always turns to face this object (invalid: no lock).</summary>
    public uint LockOrientationTo = GameObject.InvalidId;
    /// <summary>Highest price each store pays this creature for an item (-1: no limit).</summary>
    public readonly Dictionary<uint, int> MaxSellToStorePrice = new();
    /// <summary>Most classes this player may take (0: the server's limit).</summary>
    public int MulticlassLimit;
    /// <summary>NWNX_CREATURE_PROJECTILE_VFX_* for its ranged attacks, or null for the weapon's own.</summary>
    public int? RangedProjectileVfx;

    /// <summary>Attack roll modifiers by natural roll (d20); a set roll also has no automatic hit or miss.</summary>
    public readonly Dictionary<int, int> AttackRollOverride = new();
    /// <summary>Parry mode parries every attack, not one per attack of its own.</summary>
    public bool ParryAllAttacks;

    /// <summary>Settings to keep with the creature's saved form (NWNX_POS names without the plugin prefix, newest first).</summary>
    public readonly List<KeyValuePair<string, int>> Persisted = new();

    public static CreatureOverrides Of(Creature c) => c.Overrides ??= new CreatureOverrides();

    /// <summary>Identity of a creature's effects of the given types, to tell when they change.</summary>
    public static int EffectStamp(Creature c, Func<int, bool> types)
    {
        var h = new HashCode();
        foreach (var e in c.Effects)
            if (types(e.Type)) h.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(e));
        return h.ToHashCode();
    }

    /// <summary>Records the speed effects a movement factor is set against.</summary>
    public void StampMovementFactor(Creature c)
    {
        MovementFactorEffects = EffectStamp(c, IsSpeedEffect);
        MovementFactorEffectCount = c.Effects.Count(e => IsSpeedEffect(e.Type));
        MovementFactorRemovedAt = null;
    }

    /// <summary>Speed effects: increase, decrease, haste, slow.</summary>
    public static bool IsSpeedEffect(int type) => type is NwRules.EffectMovementSpeedIncrease or NwRules.EffectMovementSpeedDecrease
        or NwRules.EffectHaste or NwRules.EffectSlow;

    public static bool IsSpellResistanceEffect(int type) => type is NwRules.EffectSpellResistanceIncrease or NwRules.EffectSpellResistanceDecrease;
}

/// <summary>Rules adjustments that apply to every creature (NWNX_Creature with OBJECT_INVALID).</summary>
public sealed class CreatureOverrideSystem : IWorldSystem
{
    public CreatureOverrideSystem(World world) { }

    public readonly Dictionary<int, int> AttackRollOverride = new();
    public bool ParryAllAttacks;

    public void Step(float dt) { }
}
