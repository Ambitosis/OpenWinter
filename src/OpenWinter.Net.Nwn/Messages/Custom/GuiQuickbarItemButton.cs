using OpenWinter.Net.Nwn.Wire;

namespace OpenWinter.Net.Nwn.Messages;

public static partial class GuiQuickbarMessages
{
    /// <summary>
    /// The client setting a quickbar button to an item. Unlike the server's item buttons, which describe each item in
    /// full, it names the item and its property field, then a flag and, when set, the off-hand item of a main/off-hand
    /// pair (measured: without the flag the server reports the message short; with it a single item or a pair is
    /// stored and comes back as the server's own button).
    /// </summary>
    public sealed class GuiQuickbarSetItemButton : NwnMessage
    {
        public override byte Major => GuiQuickbarSetButton.MajorId;
        public override byte Minor => GuiQuickbarSetButton.MinorId;
        public override NwnDirection Direction => NwnDirection.ClientToServer;

        /// <summary>
        /// Whether a recorded client frame's body (after its major and minor) sets an item button, so it is read as
        /// this message rather than the schema's: the button and its kind come first, each after its type byte.
        /// </summary>
        public static bool Sets(ReadOnlySpan<byte> body) => body.Length >= 4 && body[3] == (byte)ButtonObjectType.Item;

        [NwnField("button")]
        public byte Button { get; set; }
        [NwnField("item")]
        public uint Item { get; set; }
        [NwnField("property_id")]
        public int PropertyId { get; set; }
        /// <summary>The off-hand item of a main/off-hand pair; null for a single item.</summary>
        [NwnField("secondary_item")]
        public uint? SecondaryItem { get; set; }

        public override void Read(NwnReader r)
        {
            Button = r.ReadByte();
            r.ReadInteger(NwnWireType.Byte);
            Item = r.ReadObjectId();
            PropertyId = r.ReadInt();
            // Frames recorded before the flag was known end here.
            SecondaryItem = !r.AtEnd && r.ReadBool() ? r.ReadObjectId() : null;
        }

        public override void Write(NwnWriter w)
        {
            w.WriteByte(Button);
            w.WriteInteger(NwnWireType.Byte, (long)ButtonObjectType.Item);
            w.WriteObjectId(Item);
            w.WriteInt(PropertyId);
            w.WriteBool(SecondaryItem != null);
            if (SecondaryItem is { } offHand) w.WriteObjectId(offHand);
        }
    }
}
