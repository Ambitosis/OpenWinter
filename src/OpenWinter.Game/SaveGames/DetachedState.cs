using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.SaveGames;

/// <summary>
/// One engine value (an applied effect, a queued action) as JSON outside a save game, with the save game's
/// reflection encoding (docs/SAVEGAME.md). Object references are object ids of the world that wrote it and are
/// looked up in the world that reads it; references to objects it doesn't have read as nothing. Used for the
/// object state that ObjectToJson, StoreCampaignObject and SqlBindObject write (docs/SAVEGAME.md, "Object state").
/// </summary>
public static class DetachedState
{
    public static string? Write(World world, object value, Type declared)
    {
        var w = new SnapshotWriter(world) { Detached = true };
        var node = w.Write(value, declared);
        if (node == null || SnapshotWriter.IsOmitted(node)) return null;
        w.FinishReferences();
        return node.ToJsonString();
    }

    public static T? Read<T>(World world, string json, ICollection<string>? warnings = null) where T : class
    {
        JsonNode? node;
        try { node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 1024 }); }
        catch (JsonException e) { warnings?.Add(e.Message); return null; }
        var reader = new SnapshotReader(world, new LiveObjects(world));
        T? value;
        try { value = reader.Read<T>(node); }
        catch (Exception e) when (e is InvalidOperationException or FormatException or InvalidCastException or ArgumentException or KeyNotFoundException)
        {
            warnings?.Add(e.Message);
            return null;
        }
        if (warnings != null) foreach (var w in reader.Warnings) warnings.Add(w);
        return value;
    }

    /// <summary>The world's objects by id key, as the snapshot reader looks them up.</summary>
    sealed class LiveObjects(World world) : IReadOnlyDictionary<string, GameObject>
    {
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out GameObject value)
        {
            value = uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? world.Get(id) : null;
            return value != null;
        }

        public GameObject this[string key] => TryGetValue(key, out var v) ? v : throw new KeyNotFoundException(key);
        public bool ContainsKey(string key) => TryGetValue(key, out _);
        public IEnumerable<string> Keys => world.AllObjects.Select(o => o.Id.ToString(CultureInfo.InvariantCulture));
        public IEnumerable<GameObject> Values => world.AllObjects;
        public int Count => world.AllObjects.Count();
        public IEnumerator<KeyValuePair<string, GameObject>> GetEnumerator() =>
            world.AllObjects.Select(o => new KeyValuePair<string, GameObject>(o.Id.ToString(CultureInfo.InvariantCulture), o)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
