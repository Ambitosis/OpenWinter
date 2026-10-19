namespace OpenWinter.Game.Objects;

/// <summary>The spell (or feat-granted spell) an object is casting or last cast. Spell-casting code fills it.</summary>
public sealed partial class SpellCastInfo
{
    public int SpellId = -1;
    public int CasterLevel;
    public int MetaMagic;
    /// <summary>FEAT_* the spell came from, or -1.</summary>
    public int FeatId = -1;
    /// <summary>CLASS_TYPE_* the spell was cast as, or 255 (item, innate ability).</summary>
    public int ClassType = 255;
    /// <summary>Explicit save DC (items, spell-like abilities), or -1 to compute it.</summary>
    public int SaveDC = -1;
    public uint CastItem = GameObject.InvalidId;
}

/// <summary>Damage bookkeeping (GetLastDamager, GetDamageDealtByType) and spell context shared by all objects.</summary>
public abstract partial class GameObject
{
    /// <summary>The object that last damaged this one.</summary>
    public uint LastDamager = InvalidId;
    /// <summary>Damage of the most recent hit, by DAMAGE_TYPE_* bit. Combat/effects code records it with <see cref="Rules.CreatureRules.RecordDamage"/>.</summary>
    public readonly Dictionary<int, int> LastDamageByType = new();
    /// <summary>Current or last spell cast by this object (or that created this area of effect).</summary>
    public SpellCastInfo? LastSpell;
}
