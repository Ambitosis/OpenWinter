namespace OpenWinter.Game.Objects;

/// <summary>Spell-casting additions: the target, spell level and how the spell was cast.</summary>
public sealed partial class SpellCastInfo
{
    /// <summary>GetSpellTargetObject (InvalidId for spells cast at a location).</summary>
    public uint Target = GameObject.InvalidId;
    /// <summary>GetSpellTargetLocation (the target object's location for spells cast at an object).</summary>
    public Location TargetLocation = Location.Invalid;
    /// <summary>GetLastSpellLevel: the level the spell was cast at (class spell level, domain level, or innate level); -1 unknown.</summary>
    public int SpellLevel = -1;
    /// <summary>GetSpellCastSpontaneously.</summary>
    public bool Spontaneous;
    /// <summary>Domain level when cast from a domain slot, else 0.</summary>
    public int DomainLevel;
    /// <summary>Cast instantly (ActionCastSpell bInstantSpell).</summary>
    public bool Instant;

    public SpellCastInfo Copy() => (SpellCastInfo)MemberwiseClone();
}
