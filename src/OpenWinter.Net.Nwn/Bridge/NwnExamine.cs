using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GuiInfoPopupMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Examining creatures, placeables and doors (items are the inventory's): the client's "examine" request goes as an
/// examine input, and the server's description, which also comes for ActionExamine, becomes the "examine" notify.
/// </summary>
public static class NwnExamine
{
    /// <summary>The examine input for an "examine" request the inventory didn't take (an object in the world).</summary>
    public static NwnMessage? FromClient(string kind, string json)
    {
        if (kind != "examine") return null;
        var d = JsonNode.Parse(json) as JsonObject ?? [];
        return d["object"] is JsonValue v && v.TryGetValue<uint>(out var id) && id is not (0 or GameObjUpdateMessages.InvalidObjectId)
            ? new InputMessages.InputExamine { Target = id } : null;
    }

    public static bool ToClient(NwnMessage message, NwnWorldMirror world, INwnBridgeData data, Action<byte[]> emit)
    {
        string Text(NwnTagged? description) => description?.Value switch
        {
            LocDescription l => l.Text.IsStrRef ? data.Text(l.Text.StrRef, l.Text.Gender) : l.Text.Text,
            StringDescription s => s.Text,
            _ => "",
        };
        void Notify(uint id, string kind, string name, string description, string portrait) =>
            emit(new NotifyMessage
            {
                Kind = "examine", Subject = id,
                Json = new JsonObject
                {
                    ["object"] = id, ["kind"] = kind, ["name"] = name, ["description"] = description, ["portrait"] = portrait,
                    ["icon"] = new JsonArray(), ["properties"] = new JsonArray(), ["subtitle"] = "", ["info"] = null, ["identified"] = true,
                }.ToJsonString(),
            }.Write());
        switch (message)
        {
            case GuiInfoPopupExamineCreatureData c:
                string name = c.Name?.Value is DisplayCreatureName n ? n.Name : world.NameOf(c.Creature);
                string portrait = c.PortraitId is { } pid ? NwnWorldMirror.PortraitResRef(data, pid, c.PortraitResref) : (c.PortraitResref ?? "").ToLowerInvariant();
                Notify(c.Creature, "creature", name, Text(c.Description), portrait);
                return true;
            case GuiInfoPopupExaminePlaceableData p:
                Notify(p.Placeable, "placeable", world.NameOf(p.Placeable), Text(p.Description), "");
                return true;
            case GuiInfoPopupExamineDoorData d:
                Notify(d.Door, "door", world.NameOf(d.Door), Text(d.Description), "");
                return true;
        }
        return false;
    }
}
