using System.Text.Json.Nodes;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using JsonValue = OpenWinter.Game.Scripting.JsonValue;

namespace OpenWinter.Game.Players;

/// <summary>One open NUI window on a player's client.</summary>
public sealed class NuiWindow
{
    public required int Token;
    public required Creature Player;
    public string Id = "";
    public string EventScript = "";
    /// <summary>The window definition (NuiCreate), or null for NuiCreateFromResRef.</summary>
    public JsonNode? Definition;
    public string ResRef = "";
    /// <summary>Known binds in first-seen order (from the definition, NuiSetBind, or the client).</summary>
    public readonly List<string> BindOrder = new();
    public readonly Dictionary<string, JsonNode?> Binds = new(StringComparer.Ordinal);
    /// <summary>Watched binds in the order they were watched.</summary>
    public readonly List<string> Watched = new();
    public JsonNode? UserData;

    internal void Know(string bind)
    {
        if (Binds.ContainsKey(bind)) return;
        Binds[bind] = null;
        BindOrder.Add(bind);
    }
}

/// <summary>
/// Server side of EE's NUI: windows per player, bind values, watches and user
/// data. Clients are told about windows and binds through notifies
/// ("nui-open", "nui-close", "nui-bind", "nui-group"); client input comes back
/// through <see cref="HandleEvent"/> and <see cref="ClientSetBind"/>.
/// </summary>
public sealed class NuiService : IWorldSystem
{
    /// <summary>Event context keys read by NuiGetEvent*.</summary>
    public const string KeyPlayer = "NuiPlayer", KeyToken = "NuiToken", KeyType = "NuiType", KeyElement = "NuiElement",
        KeyArrayIndex = "NuiArrayIndex", KeyPayload = "NuiPayload";

    readonly World _world;
    readonly Dictionary<uint, List<NuiWindow>> _windows = new();
    int _nextToken = 1;

    public NuiService(World world)
    {
        _world = world;
        world.ObjectRemoved += o => _windows.Remove(o.Id);
    }

    public void Step(float dt) { }

    public IReadOnlyList<NuiWindow> Windows(Creature player) =>
        _windows.TryGetValue(player.Id, out var list) ? list : Array.Empty<NuiWindow>();

    public NuiWindow? Get(Creature? player, int token) =>
        player != null && _windows.TryGetValue(player.Id, out var list) ? list.FirstOrDefault(w => w.Token == token) : null;

    public NuiWindow? Find(Creature player, string id) =>
        id.Length == 0 ? null : Windows(player).FirstOrDefault(w => w.Id == id);

    /// <summary>NuiCreate / NuiCreateFromResRef. Returns the token, or 0.</summary>
    public int Create(Creature player, JsonNode? definition, string resref, string id, string eventScript)
    {
        if (!player.IsPC || player.IsDestroyed) return 0;
        if (definition == null && resref.Length == 0) return 0;
        if (definition != null && definition is not JsonObject) return 0;
        if (Find(player, id) is { } old) Destroy(player, old.Token);
        var w = new NuiWindow
        {
            Token = _nextToken++,
            Player = player,
            Id = id,
            EventScript = eventScript,
            Definition = definition?.DeepClone(),
            ResRef = resref,
        };
        foreach (var b in BindsIn(w.Definition)) w.Know(b);
        if (!_windows.TryGetValue(player.Id, out var list)) _windows[player.Id] = list = new List<NuiWindow>();
        list.Add(w);
        _world.Output?.Notify("nui-open", player, new { token = w.Token, id, resref, window = w.Definition?.DeepClone() }, player);
        return w.Token;
    }

    /// <summary>NuiDestroy: drops all server state and tells the client to close; no close event.</summary>
    public void Destroy(Creature player, int token)
    {
        if (Get(player, token) is not { } w) return;
        _windows[player.Id].Remove(w);
        _world.Output?.Notify("nui-close", player, new { token }, player);
    }

    /// <summary>Bind names referenced anywhere in a NUI definition ({"bind": "name"} objects).</summary>
    public static IEnumerable<string> BindsIn(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                if (o.Count == 1 && o.TryGetPropertyValue("bind", out var b) && b is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var name))
                {
                    yield return name;
                    yield break;
                }
                foreach (var kv in o)
                    foreach (var n in BindsIn(kv.Value)) yield return n;
                break;
            case JsonArray a:
                foreach (var item in a)
                    foreach (var n in BindsIn(item)) yield return n;
                break;
        }
    }

    public JsonNode? GetBind(Creature player, int token, string bind) =>
        Get(player, token) is { } w && w.Binds.TryGetValue(bind, out var v) ? v?.DeepClone() : null;

    /// <summary>NuiSetBind: stores the value, syncs it to the client and fires "watch" if watched.</summary>
    public void SetBind(Creature player, int token, string bind, JsonNode? value)
    {
        if (Get(player, token) is not { } w) return;
        w.Know(bind);
        w.Binds[bind] = value?.DeepClone();
        _world.Output?.Notify("nui-bind", player, new { token, bind, value = value?.DeepClone() }, player);
        if (w.Watched.Contains(bind)) RunEvent(w, "watch", bind, -1, null);
    }

    /// <summary>
    /// A bind value changed on the client (user input): the whole value, and the row whose element changed (-1 for
    /// none), which a "watch" event, fired if watched, reports as its array index (visual scene nuibindindex).
    /// </summary>
    public void ClientSetBind(Creature player, int token, string bind, JsonNode? value, int arrayIndex = -1)
    {
        if (Get(player, token) is not { } w) return;
        w.Know(bind);
        bool changed = !Scripting.Functions.JsonExtraFunctions.Equal(w.Binds[bind], value);
        w.Binds[bind] = value?.DeepClone();
        if (changed && w.Watched.Contains(bind)) RunEvent(w, "watch", bind, arrayIndex, null);
    }

    public bool SetWatch(Creature player, int token, string bind, bool watch)
    {
        if (Get(player, token) is not { } w) return false;
        if (watch)
        {
            w.Know(bind);
            if (!w.Watched.Contains(bind)) w.Watched.Add(bind);
        }
        else w.Watched.Remove(bind);
        return true;
    }

    public string NthBind(Creature player, int token, bool watched, int nth)
    {
        if (Get(player, token) is not { } w || nth < 0) return "";
        var list = watched ? w.Watched : w.BindOrder;
        return nth < list.Count ? list[nth] : "";
    }

    public void SetGroupLayout(Creature player, int token, string element, JsonNode? layout)
    {
        if (Get(player, token) is not { } w) return;
        foreach (var b in BindsIn(layout)) w.Know(b);
        _world.Output?.Notify("nui-group", player, new { token, element, layout = layout?.DeepClone() }, player);
    }

    /// <summary>
    /// A NUI event from the client: runs the window's event script (or the
    /// module's OnNuiEvent) with the event context NuiGetEvent* read. A "close"
    /// event also drops the window's server-side state afterwards.
    /// </summary>
    public void HandleEvent(Creature player, int token, string eventType, string element, int arrayIndex, JsonValue? payload)
    {
        if (Get(player, token) is not { } w) return;
        RunEvent(w, eventType, element, arrayIndex, payload?.Node);
        if (eventType == "close" && Get(player, token) is { } still) _windows[player.Id].Remove(still);
    }

    void RunEvent(NuiWindow w, string type, string element, int arrayIndex, JsonNode? payload)
    {
        if (_world.Scripts == null) return;
        var ctx = new EventContext()
            .Set(KeyPlayer, w.Player.Id)
            .Set(KeyToken, w.Token)
            .Set(KeyType, type)
            .Set(KeyElement, element)
            .Set(KeyArrayIndex, arrayIndex)
            .Set(KeyPayload, new JsonValue(payload?.DeepClone()));
        var script = w.EventScript.Length > 0 ? w.EventScript : _world.Module.Script("Mod_OnNuiEvent");
        if (script.Length > 0) _world.Scripts.Run(script, _world.Module.Id, ctx);
    }
}
