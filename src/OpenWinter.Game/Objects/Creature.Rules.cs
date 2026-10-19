namespace OpenWinter.Game.Objects;

/// <summary>A spell-like ability (SpecAbilityList entry).</summary>
public sealed class SpellAbility
{
    public int Spell;
    public int CasterLevel;
    public bool Ready = true;
}

/// <summary>
/// Creature rules state that isn't in the UTC/BIC basics (CreatureStats.cs): hit point
/// bookkeeping, spell slots, modes, associates. Filled from the template by
/// <see cref="Rules.CreatureRules.EnsureInit"/>.
/// </summary>
public sealed partial class Creature
{
    /// <summary>True once rules state has been read from the template.</summary>
    public bool RulesInitialised;
    /// <summary>Sum of hit dice rolls without Constitution or feats (BIC/UTC "HitPoints").</summary>
    public int BaseHitPoints;
    /// <summary>Template max hit points minus the rules computation at load (keeps toolset values authoritative).</summary>
    public int HitPointOffset;
    /// <summary>Footstep type override (FOOTSTEP_TYPE_*), or -1 for the appearance default.</summary>
    public int FootstepOverride = -1;
    public int StartingPackage = 255;
    public int FamiliarType = 255, CompanionType = 255;
    public string FamiliarName = "", CompanionName = "";
    public bool IsResting;
    public bool SelectableWhenDead;
    /// <summary>ACTION_MODE_* currently on (stealth and detect also mirror StealthMode/DetectMode).</summary>
    public readonly HashSet<int> ActionModes = new();
    public readonly List<SpellAbility> SpellAbilities = new();
    /// <summary>
    /// CLASS_TYPE_* of the creature's last spell cast that reached its commit point (255: a spell-like ability; -1:
    /// none yet, or a cast from no class). A failed damage Concentration check takes its toll from this class
    /// (nwserver conc_previous). Not saved.
    /// </summary>
    public int LastCommittedCastClass = -1;
    /// <summary>Spontaneous casters' remaining slots: (index in Classes, spell level) to uses left.</summary>
    public readonly Dictionary<(int ClassIndex, int Level), int> SpellSlotsLeft = new();
    /// <summary>Memorised slots holding domain spells: (index in Classes, spell level, slot index).</summary>
    public readonly HashSet<(int ClassIndex, int Level, int Slot)> DomainSlots = new();
    /// <summary>SetObjectHiliteColor value, or -1.</summary>
    public int HiliteColor = -1;
    /// <summary>The polymorph the creature is under (or changing back from), with the equipment it put away; null if none.</summary>
    public Rules.PolymorphState? PolymorphForm;
}
