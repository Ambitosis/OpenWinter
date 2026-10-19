using OpenWinter.Net;

namespace OpenWinter.Client.Gameplay;

/// <summary>
/// Which objects the pointer and the highlight key light up, and which a click can pick. The original lights up and
/// names every dead creature, even a corpse that can't be selected (the Prelude's goblins in Pavel's room are dead,
/// not destroyable and not selectable when dead, and still glow and read "Dead Goblin / Dead" under the highlight key:
/// nwmain scene corpsehilite, docs/DEATH_AND_DYING.md "Highlighting corpses"). Clicks only take selectable objects.
/// </summary>
public static class PickRules
{
    /// <summary>A click (left or right) or a targeting mode can pick it: the server's Selectable flag, or a door.</summary>
    public static bool Selectable(ObjectKind kind, ObjectFlags flags)
        => (flags & ObjectFlags.Selectable) != 0 || kind == ObjectKind.Door;

    /// <summary>The pointer highlights and names it, and the highlight key lights it: anything selectable, and every corpse.</summary>
    public static bool Highlightable(ObjectKind kind, ObjectFlags flags)
        => Selectable(kind, flags) || kind == ObjectKind.Creature && (flags & ObjectFlags.Dead) != 0;
}
