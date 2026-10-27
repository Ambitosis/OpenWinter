using OpenWinter.Net.Nwn.Wire;

namespace OpenWinter.Net.Nwn.Messages;

public static partial class GuiQuickbarMessages
{
    /// <summary>
    /// The client setting a quickbar button to an item: unlike the server's item buttons, which describe the item in
    /// full, it names only the item and its property field (measured: a bool-prefixed or fully described item is
    /// refused or breaks the message, the item's id and property alone are stored as the server's own).
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

        public override void Read(NwnReader r)
        {
            Button = r.ReadByte();
            r.ReadInteger(NwnWireType.Byte);
            Item = r.ReadObjectId();
            PropertyId = r.ReadInt();
        }

        public override void Write(NwnWriter w)
        {
            w.WriteByte(Button);
            w.WriteInteger(NwnWireType.Byte, (long)ButtonObjectType.Item);
            w.WriteObjectId(Item);
            w.WriteInt(PropertyId);
        }
    }
}
