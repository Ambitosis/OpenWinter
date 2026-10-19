namespace OpenWinter.Content;

/// <summary>A quickbar action, independent of the character-file format and client interface.</summary>
public class QuickbarSlot
{
    public string Kind { get; set; } = "";
    public int Id { get; set; }
    public int Class { get; set; } = -1;
    public int Meta { get; set; } = 255;
    public int Sub { get; set; }
    public uint Item { get; set; }
    /// <summary>A compound main/off-hand equipment binding.</summary>
    public bool Paired { get; set; }
    public uint SecondaryItem { get; set; }
    /// <summary>Item property index; -1 selects the first power or equips the item.</summary>
    public int Prop { get; set; } = -1;
    public string Arg { get; set; } = "";
    /// <summary>Remembered item name, used when object IDs change between sessions; a text macro's label.</summary>
    public string Name { get; set; } = "";
    /// <summary>A text macro's command line ("macro" slots: what it sends to chat, docs/QUICKBAR_RADIAL.md).</summary>
    public string Text { get; set; } = "";

    /// <summary>Remembered power-item presentation and matching identity after its last copy is consumed.</summary>
    public string[] ItemIcons { get; set; } = [];
    public string ItemKey { get; set; } = "";
    public string ItemPowerKey { get; set; } = "";
    public bool ItemHasPower { get; set; }

    public QuickbarSlot Copy() => (QuickbarSlot)MemberwiseClone();
}
