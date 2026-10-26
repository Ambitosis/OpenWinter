using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx;

/// <summary>
/// A player's timing bar (NWNX_EVENTS_TIMING_BAR_*: 1-5 the trap skills, 6 rest, 10 a script's own):
/// NWNX_ON_TIMING_BAR_START (EVENT_ID, DURATION in milliseconds) as it begins, then _STOP when it runs out or
/// _CANCEL when it is broken off. Bars the server times end on the server only.
/// </summary>
public static class NwnxTimingBar
{
    sealed class Bar { public bool Running; }

    static readonly ConditionalWeakTable<Creature, Bar> _bars = new();

    public static void Start(World world, Creature pc, int eventId, int milliseconds)
    {
        if (!pc.IsPC) return;
        _bars.GetValue(pc, _ => new Bar()).Running = true;
        var ev = NwnxEvent.Begin(world, "NWNX_ON_TIMING_BAR_START", pc)?.Add("EVENT_ID", eventId).Add("DURATION", milliseconds);
        ev?.Before();
        ev?.After();
    }

    /// <summary>The bar ran out (<paramref name="cancelled"/> false) or was broken off.</summary>
    public static void Ended(World world, Creature pc, bool cancelled)
    {
        if (!_bars.TryGetValue(pc, out var bar) || !bar.Running) return;
        bar.Running = false;
        var ev = NwnxEvent.Begin(world, cancelled ? "NWNX_ON_TIMING_BAR_CANCEL" : "NWNX_ON_TIMING_BAR_STOP", pc);
        ev?.Before();
        ev?.After();
    }
}
