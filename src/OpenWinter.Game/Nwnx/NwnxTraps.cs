using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx;

/// <summary>NWNX_ON_TRAP_ENTER bookkeeping: once per stay in a trap trigger, also for creatures it doesn't go off for.</summary>
public static class NwnxTraps
{
    static readonly ConditionalWeakTable<Trigger, HashSet<uint>> _inside = new();

    /// <summary>The event's _BEFORE half run for a creature stepping in (null if it already did, or nobody listens).</summary>
    public static NwnxEvent? Entering(World world, Trigger t, Creature c)
    {
        if (!Plugins.EventsPlugin.Listening(world, "NWNX_ON_TRAP_ENTER")) return null;
        if (!_inside.GetValue(t, _ => new()).Add(c.Id)) return null;
        var ev = NwnxEvent.Begin(world, "NWNX_ON_TRAP_ENTER", c)?.Add("TRAP_OBJECT_ID", t).Add("TRAP_FORCE_SET", false);
        ev?.Before();
        return ev;
    }

    public static void Left(Trigger t, Creature c)
    {
        if (_inside.TryGetValue(t, out var set)) set.Remove(c.Id);
    }
}
