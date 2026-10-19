namespace OpenWinter.Game.Objects;

/// <summary>Dungeon Master state on creatures: the DM avatar and creatures a DM possesses.</summary>
public sealed partial class Creature
{
    /// <summary>The DM session this creature is the avatar of (set even while the DM possesses something else).</summary>
    public PlayerSession? DMOwner;
    /// <summary>A DM avatar that players can't see or perceive (the default; "appear" turns it off).</summary>
    public bool DMHidden;
    /// <summary>The DM avatar possessing this creature, or InvalidId.</summary>
    public uint DMPossessor = InvalidId;
    /// <summary>The DM turned this creature's AI off: it stands still and ignores its queue.</summary>
    public bool DMAIOff;

    /// <summary>The creature is a DM's own character (GetIsDM): a DM avatar, or a player creature with DM rights.</summary>
    public bool IsDMAvatar => DMOwner != null || Player?.IsDM == true && DMPossessor == InvalidId;
    public bool IsDMPossessed => DMPossessor != InvalidId;
    /// <summary>A hidden DM avatar: not sent to players' clients, not perceived, can't trigger traps or encounters.</summary>
    public bool IsDMUndetectable => DMOwner != null && DMHidden;
}
