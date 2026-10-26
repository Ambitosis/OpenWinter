using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Areas;

public sealed partial class Area
{
    /// <summary>
    /// The creature whose OnEnter (OnExit) event the area last ran. Set when the event runs, not when the creature
    /// arrives: right after CreateObject in the same script it is still the previous one (parity suite nwnx_area_props).
    /// </summary>
    public uint LastEntered = InvalidId, LastLeft = InvalidId;

    /// <summary>Notes the creature of an OnEnter or OnExit event as the area runs it.</summary>
    internal void NoteEntryEvent(string field, EventContext ctx)
    {
        if (field == "OnEnter") LastEntered = ctx.Obj("Entering");
        else if (field == "OnExit") LastLeft = ctx.Obj("Exiting");
    }

    /// <summary>Drops the cached pathfinding grids (their size follows the area's).</summary>
    internal void ResetPathfinders()
    {
        _pathfinder = null;
        _movementPathfinder = null;
    }
}
