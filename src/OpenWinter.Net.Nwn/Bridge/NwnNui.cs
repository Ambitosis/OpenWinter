using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.NuiMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Script-built NUI windows: the server's create, destroy, bind and layout messages become the client's "nui-open",
/// "nui-close", "nui-bind" and "nui-group" notifies, and the client's "nui-event" and "nui-bind" requests go back as
/// NUI event and bind messages.
/// </summary>
public static class NwnNui
{
    public static bool ToClient(NwnMessage message, Action<byte[]> emit)
    {
        void Notify(string kind, JsonObject json) => emit(new NotifyMessage { Kind = kind, Json = json.ToJsonString() }.Write());
        switch (message)
        {
            case NuiCreateWindow m:
                Notify("nui-open", new JsonObject
                {
                    ["token"] = m.Token, ["id"] = m.WindowId, ["resref"] = m.Resref, ["window"] = Parse(m.Data),
                });
                return true;
            case NuiDestroyWindow m:
                Notify("nui-close", new JsonObject { ["token"] = m.Token });
                return true;
            case NuiBinds m:
                foreach (var b in m.Binds)
                    Notify("nui-bind", new JsonObject { ["token"] = b.Token, ["bind"] = b.Bind, ["value"] = Parse(b.Value) });
                return true;
            case NuiSetLayout m:
                Notify("nui-group", new JsonObject { ["token"] = m.Token, ["element"] = m.Element, ["layout"] = Parse(m.Data) });
                return true;
        }
        return false;
    }

    /// <summary>The NUI message for a client request, or null when it isn't one.</summary>
    public static NwnMessage? FromClient(string kind, string json)
    {
        if (kind is not ("nui-event" or "nui-bind")) return null;
        var d = JsonNode.Parse(json) as JsonObject ?? [];
        int Int(string key, int fallback = 0) => d[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
        string Str(string key) => d[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
        string Json(string key) => d[key]?.ToJsonString() ?? "null";
        if (kind == "nui-bind")
            return new NuiBinds { Binds = [new NuiBindEntry { Token = Int("token"), Bind = Str("bind"), Value = Json("value") }] };
        return new NuiEvents
        {
            Events = [new NuiEventEntry { Token = Int("token"), Event = Str("type"), Element = Str("element"), ArrayIndex = Int("index", -1), Payload = Json("payload") }],
        };
    }

    static JsonNode? Parse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonNode.Parse(json); }
        catch (JsonException) { return null; }
    }
}
