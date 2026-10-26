namespace OpenWinter.Game.Combat;

/// <summary>The weapon feats a base item can be given beyond its baseitems.2da columns.</summary>
public enum WeaponFeat
{
    Focus, EpicFocus, GreaterFocus, Specialization, EpicSpecialization, GreaterSpecialization,
    ImprovedCritical, OverwhelmingCritical, DevastatingCritical, WeaponOfChoice,
}

/// <summary>
/// Weapon rules a module changes at run time (NWNX_Weapon): more feats per base item, finesse sizes, base items
/// that count as unarmed for finesse or as monk weapons, greater focus and specialization bonuses, ranged attack
/// distances and one-and-a-half Strength weapons.
/// </summary>
public sealed class WeaponRuleOverrides
{
    readonly Dictionary<(int BaseItem, WeaponFeat Kind), List<int>> _feats = new();
    /// <summary>Smallest creature size that can finesse the base item, overriding baseitems.2da.</summary>
    public readonly Dictionary<int, int> FinesseSize = new();
    /// <summary>Base items a module marked as unarmed for Weapon Finesse (kept; on nwserver the mark changes nothing).</summary>
    public readonly HashSet<int> FinesseAsUnarmed = new();
    /// <summary>Base items that count as monk weapons besides those baseitems.2da marks.</summary>
    public readonly HashSet<int> MonkWeapons = new();
    public int GreaterFocusAttackBonus = 1, GreaterSpecializationDamageBonus = 2;
    /// <summary>Ranged attack distances by base item: the most, the passive (new target) and the preferred one.</summary>
    public readonly Dictionary<int, (float Max, float Passive, float Preferred)> RangedDistance = new();
    /// <summary>Items that add half their wielder's Strength bonus again to damage (by object id).</summary>
    public readonly HashSet<uint> OneHalfStrength = new();

    public bool Any { get; private set; }

    public void AddFeat(int baseItem, WeaponFeat kind, int feat)
    {
        if (!_feats.TryGetValue((baseItem, kind), out var list)) _feats[(baseItem, kind)] = list = new List<int>();
        if (!list.Contains(feat)) list.Add(feat);
        Any = true;
    }

    public IReadOnlyList<int> Feats(int baseItem, WeaponFeat kind) =>
        Any && _feats.TryGetValue((baseItem, kind), out var list) ? list : Array.Empty<int>();
}
