using System.Diagnostics;
using Godot;

namespace OpenWinter.Client.Gameplay;

/// <summary>
/// Wall-clock marks through one area load, printed when the area is handed to the player ("[client] area ... ready"):
/// the load message, the synchronous build (tiles, objects), the server's snapshot and area-ready. For profiling
/// area transitions against the original (about 0.3 s from leaving to appearing; docs/INTER_AREA_MOVEMENT.md).
/// </summary>
public static class AreaLoadTiming
{
    static readonly Stopwatch Clock = new();
    static readonly List<(string Mark, double Ms)> Marks = new();
    static string _area = "";

    public static void Begin(string area)
    {
        _area = area;
        Marks.Clear();
        Totals.Clear();
        Clock.Restart();
    }

    public static void Mark(string mark)
    {
        if (Clock.IsRunning) Marks.Add((mark, Clock.Elapsed.TotalMilliseconds));
    }

    static readonly Dictionary<string, (double Ms, int Count)> Totals = new();

    /// <summary>Whether a load is being timed.</summary>
    public static bool Running => Clock.IsRunning;

    /// <summary>Adds one item's time to a running total ("name*" in the report, with the count).</summary>
    public static void Add(string name, double ms)
    {
        if (!Clock.IsRunning) return;
        Totals[name] = Totals.TryGetValue(name, out var t) ? (t.Ms + ms, t.Count + 1) : (ms, 1);
    }

    /// <summary>Times a phase: adds "name" with its own duration.</summary>
    public static IDisposable Phase(string name) => new Scope(name);

    public static void End()
    {
        if (!Clock.IsRunning) return;
        Mark("ready");
        Clock.Stop();
        GD.Print($"[client] area {_area} " + string.Join(", ", Marks.Select(m => $"{m.Mark} {m.Ms:0}")) + " ms"
            + (Totals.Count > 0 ? " (" + string.Join(", ", Totals.Select(t => $"{t.Key}* {t.Value.Ms:0}/{t.Value.Count}")) + ")" : ""));
    }

    sealed class Scope(string name) : IDisposable
    {
        readonly double _start = Clock.Elapsed.TotalMilliseconds;
        public void Dispose() { if (Clock.IsRunning) Marks.Add((name + "+", Clock.Elapsed.TotalMilliseconds - _start)); }
    }
}
