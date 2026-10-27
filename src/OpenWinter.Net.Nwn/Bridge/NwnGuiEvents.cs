using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Wire;
using OpenWinter.Net.Nwn.Messages;
using static OpenWinter.Net.Nwn.Messages.GuiEventMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Built-in GUI panels and events: the server's SetGuiPanelDisabled and PopUpGUIPanel messages become the client's
/// "gui-disable" and "gui-panel" notifies, and the client's "gui-event" requests go back as GUI event messages
/// (the module's OnPlayerGuiEvent).
/// </summary>
public static class NwnGuiEvents
{
    public static bool ToClient(NwnMessage message, Action<byte[]> emit)
    {
        void Notify(string kind, JsonObject json) => emit(new NotifyMessage { Kind = kind, Json = json.ToJsonString() }.Write());
        switch (message)
        {
            case GuiEventDisable m:
                Notify("gui-disable", new JsonObject { ["panel"] = (int)m.GuiElement, ["disabled"] = m.Disable, ["target"] = m.GuiEventObject });
                return true;
            case PopupGuiPanelMessages.PopUpGUIPanelPopUpOtherPlayerPanel m:
                Notify("gui-panel", new JsonObject { ["panel"] = m.GuiPanel });
                return true;
        }
        return false;
    }

    /// <summary>The GUI event message for a "gui-event" request, or null when it isn't one.</summary>
    public static NwnMessage? FromClient(string kind, string json)
    {
        if (kind != "gui-event") return null;
        var d = JsonNode.Parse(json) as JsonObject ?? [];
        long Int(string key) => d[key] is JsonValue v && v.TryGetValue<long>(out var i) ? i : 0;
        float F(JsonNode? n) => n is JsonValue v && v.TryGetValue<float>(out var f) ? f : 0;
        uint target = d["object"] is JsonValue o && o.TryGetValue<long>(out var id) && id is > 0 and <= uint.MaxValue
            ? (uint)id : GameObjUpdateMessages.InvalidObjectId;
        var vector = d["vector"] switch
        {
            JsonArray a when a.Count >= 3 => (F(a[0]), F(a[1]), F(a[2])),
            JsonObject v => (F(v["x"]), F(v["y"]), F(v["z"])),
            _ => (0f, 0f, 0f),
        };
        return new GuiEventNotify
        {
            GuiEvent = (ushort)Int("type"), GuiEventInteger = unchecked((ushort)Int("integer")), GuiEventObject = target, GuiEventPosition = vector,
        };
    }
}
