using System.Numerics;
using System.Reflection;
using System.Text.Json;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Requests;

/// <summary>
/// Marks a static handler for a client UI request kind ("equip", "store-buy", "nui-event", ...).
/// Handlers are found by reflection, like engine functions, so a package adds requests
/// without touching shared files. Signature: <c>static void Name(Request r)</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ClientRequestAttribute(string kind) : Attribute
{
    public string Kind { get; } = kind;
}

/// <summary>One request from a player's client: what the player asked for and its JSON arguments.</summary>
public sealed class Request
{
    public required World World;
    public required Creature Player;
    public required string Kind;
    public required JsonElement Data;

    public int Int(string name, int fallback = 0) =>
        Data.ValueKind == JsonValueKind.Object && Data.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : fallback;
    public float Float(string name, float fallback = 0) =>
        Data.ValueKind == JsonValueKind.Object && Data.TryGetProperty(name, out var v) && v.TryGetSingle(out var f) ? f : fallback;
    public string Str(string name) =>
        Data.ValueKind == JsonValueKind.Object && Data.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    public bool Bool(string name) =>
        Data.ValueKind == JsonValueKind.Object && Data.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    public JsonElement? Element(string name) =>
        Data.ValueKind == JsonValueKind.Object && Data.TryGetProperty(name, out var v) ? v : null;
    public uint Id(string name) =>
        Data.ValueKind == JsonValueKind.Object && Data.TryGetProperty(name, out var v) && v.TryGetUInt32(out var u) ? u : GameObject.InvalidId;
    public T? Obj<T>(string name) where T : GameObject => World.Get(Id(name)) as T;
    public Vector3 Vec(string name)
    {
        if (Element(name) is not { ValueKind: JsonValueKind.Array } a || a.GetArrayLength() < 3) return Vector3.Zero;
        return new Vector3(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());
    }

    /// <summary>True if the object is something this player may act on directly (themselves or an item they own).</summary>
    public bool Owns(Item item) => World.Inventory.Owner(item) == Player;
}

/// <summary>Dispatches client requests to their handlers.</summary>
public static class ClientRequests
{
    static readonly Dictionary<string, Action<Request>> Handlers = Discover();

    static Dictionary<string, Action<Request>> Discover()
    {
        var map = new Dictionary<string, Action<Request>>(StringComparer.Ordinal);
        foreach (var type in typeof(ClientRequests).Assembly.GetTypes())
        foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = m.GetCustomAttribute<ClientRequestAttribute>();
            if (attr == null) continue;
            if (map.ContainsKey(attr.Kind)) throw new InvalidOperationException($"Client request '{attr.Kind}' is handled twice ({type.Name}.{m.Name})");
            map[attr.Kind] = m.CreateDelegate<Action<Request>>();
        }
        return map;
    }

    public static IEnumerable<string> Kinds => Handlers.Keys;

    /// <summary>Runs the handler for a request. Unknown kinds and malformed data are ignored (clients are untrusted).</summary>
    public static bool Handle(World world, Creature player, string kind, string json)
    {
        if (!Handlers.TryGetValue(kind, out var handler)) return false;
        JsonElement data;
        try { data = json.Length == 0 ? default : JsonDocument.Parse(json).RootElement; }
        catch (JsonException) { return false; }
        try
        {
            handler(new Request { World = world, Player = player, Kind = kind, Data = data });
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or FormatException or IndexOutOfRangeException)
        {
            world.Scripts?.Log($"[request] {kind} from {player.Name}: {e.Message}");
            return false;
        }
    }
}
