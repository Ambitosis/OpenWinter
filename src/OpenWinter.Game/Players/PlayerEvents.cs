using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Players;

/// <summary>The last OnPlayerGuiEvent (GetLastGuiEvent*).</summary>
public sealed record GuiEvent(uint Player, int Type, int Integer, uint Object, Vector3 Vector);

/// <summary>The last OnPlayerTarget selection (GetLastPlayerToSelectTarget, GetTargetingModeSelected*).</summary>
public sealed record TargetSelection(uint Player, uint Target, Vector3 Position);

/// <summary>
/// Module events raised by player input. The server calls these when the
/// client reports the corresponding action; scripts read the data through the
/// GetLastGuiEvent* / GetTargetingModeSelected* functions.
/// </summary>
public static class PlayerEvents
{
    /// <summary>Raises the module OnPlayerGuiEvent (GUIEVENT_* type).</summary>
    public static void GuiEvent(World world, Creature player, int type, int integer = 0, uint obj = GameObject.InvalidId, Vector3 vector = default)
    {
        world.LastGuiEvent = new GuiEvent(player.Id, type, integer, obj, vector);
        world.RunEvent(world.Module, "Mod_OnPlrGuiEvt", new EventContext());
    }

    /// <summary>
    /// Raises the module OnPlayerTarget after EnterTargetingMode. Pass the area
    /// as target for ground clicks, or InvalidId when the player cancelled.
    /// </summary>
    public static void TargetSelected(World world, Creature player, uint target, Vector3 position)
    {
        world.LastTarget = new TargetSelection(player.Id, target, position);
        world.RunEvent(world.Module, "Mod_OnPlrTarget", new EventContext());
    }
}
