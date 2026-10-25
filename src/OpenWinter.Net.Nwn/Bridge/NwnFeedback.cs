using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.ClientSideMessageFeedbackMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Feedback the server sends by number, written into the chat window with the talk table's text the original
/// shows for it. Feedback without a known line is left out.
/// </summary>
public static class NwnFeedback
{
    // Talk table lines, as the original's chat shows them: "Acquired Item: <CUSTOM0>", "Lost Item: <CUSTOM0>",
    // the gold gained and lost lines, "This object is locked." and the "you need a key" line.
    const uint ItemReceived = 10468, ItemLost = 10469, GoldGained = 62489, GoldLost = 62490, Locked = 8296, KeyNeeded = 7944;

    public static void ToClient(ClientSideMessageFeedback message, INwnBridgeData data, Action<byte[]> emit)
    {
        string Line(uint strRef, string custom = "") => data.Text(strRef, 0).Replace("<CUSTOM0>", custom);
        var text = message.Payload.Value switch
        {
            FeedbackItemReceived m => Line(ItemReceived, ItemName(m.Item, data)),
            FeedbackItemLost m => Line(ItemLost, ItemName(m.Item, data)),
            FeedbackItemGoldGained m => Line(GoldGained, m.GoldAmount.ToString()),
            FeedbackItemGoldLost m => Line(GoldLost, m.GoldAmount.ToString()),
            FeedbackObjectLocked => Line(Locked),
            FeedbackObjectSpecialKey => Line(KeyNeeded),
            FeedbackSendMessageToPc m => m.Message,
            FeedbackSendMessageToPcStrref m => Line((uint)m.Strref),
            _ => null,
        };
        if (!string.IsNullOrEmpty(text)) emit(new ChatMessage { Channel = ChatChannel.Server, Text = text }.Write());
    }

    /// <summary>An item's name in feedback: its own when identified, otherwise its base item's.</summary>
    static string ItemName(ItemData item, INwnBridgeData data) => item.Name?.Value switch
    {
        NwnLocString { IsStrRef: true } l => data.Text(l.StrRef, l.Gender),
        NwnLocString l => l.Text,
        string s => s,
        _ => item.BaseItemId is { } baseItem && uint.TryParse(data.TwoDA("baseitems", (int)baseItem, "Name"), out var strRef)
            ? data.Text(strRef, 0)
            : "",
    };
}
