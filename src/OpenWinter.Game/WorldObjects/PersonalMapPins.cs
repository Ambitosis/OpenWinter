using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;

namespace OpenWinter.Game.WorldObjects;

/// <summary>Personal notes in an OpenWinter world save; native BIC map-pin import/export is separate.</summary>
public sealed record PersonalMapPin(string Id, string Note, float X, float Y);

public sealed partial class AreaEnvironment
{
    public Dictionary<uint, List<PersonalMapPin>> PersonalMapPins = new();
}

/// <summary>Personal map edits are scoped to the authenticated player and their current area.</summary>
public static class PersonalMapPins
{
    // Transport/storage bounds, not claims about native UI limits.
    public const int MaxNoteLength = 4096, MaxPinsPerArea = 1024, MaxPinsPerPlayer = 1024;

    /// <summary>A player's pins across every area (areas never loaded hold none).</summary>
    static int PinCount(World world, Creature pc) =>
        world.Areas.Sum(a => a.Environment?.PersonalMapPins.GetValueOrDefault(pc.Id)?.Count ?? 0);

    public static IReadOnlyList<PersonalMapPin> Read(World world, Area area, Creature pc) =>
        AreaSettings.Of(world, area).PersonalMapPins.TryGetValue(pc.Id, out var pins) ? pins : [];

    static Area? CurrentArea(Request r) => r.Player.IsPC && r.Player.Area is { } area && r.Id("area") == area.Id ? area : null;

    [ClientRequest("map-pin-add")]
    static void Add(Request r)
    {
        if (CurrentArea(r) is not { } area) return;
        float x = r.Float("x", float.NaN), y = r.Float("y", float.NaN);
        string note = r.Str("note");
        if (!float.IsFinite(x) || !float.IsFinite(y) || x < 0 || y < 0
            || x >= area.Width * Area.TileSize || y >= area.Height * Area.TileSize || note.Length > MaxNoteLength) return;
        var all = AreaSettings.Of(r.World, area).PersonalMapPins;
        if (!all.TryGetValue(r.Player.Id, out var pins)) all[r.Player.Id] = pins = [];
        if (pins.Count >= MaxPinsPerArea || PinCount(r.World, r.Player) >= MaxPinsPerPlayer) return;
        pins.Add(new PersonalMapPin(Guid.NewGuid().ToString("N"), note, x, y));
        MapExploration.Send(r.World, r.Player);
    }

    [ClientRequest("map-pin-edit")]
    static void Edit(Request r) => Change(r, remove: false);

    [ClientRequest("map-pin-remove")]
    static void Remove(Request r) => Change(r, remove: true);

    static void Change(Request r, bool remove)
    {
        if (CurrentArea(r) is not { } area || !AreaSettings.Of(r.World, area).PersonalMapPins.TryGetValue(r.Player.Id, out var pins)) return;
        int index = pins.FindIndex(p => p.Id == r.Str("id"));
        if (index < 0) return;
        if (remove) pins.RemoveAt(index);
        else
        {
            string note = r.Str("note");
            if (note.Length > MaxNoteLength) return;
            pins[index] = pins[index] with { Note = note };
        }
        MapExploration.Send(r.World, r.Player);
    }
}
