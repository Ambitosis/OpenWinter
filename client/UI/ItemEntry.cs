using System.Text.Json;

namespace OpenWinter.Client.UI;

/// <summary>One line of an item's properties or requirements in a tooltip.</summary>
public sealed record ItemLine(string Text, bool Bad = false, bool Temporary = false, bool Met = true);

/// <summary>
/// The tooltip details the server sends with every item ("info"): base type and category, weapon or
/// armour figures, properties, requirements, charges, weight (tenths of a pound), value and flags.
/// </summary>
public sealed record ItemInfo
{
    public string Type = "", Category = "misc", Damage = "", DamageType = "", Critical = "";
    public int Panel = 1, AC = -1, Charges = -1, Weight, Value = -1, MaxStack = 1, Width = 1, Height = 1, Slots;
    public string ArmorWeight = "";
    public string? InventorySound;
    public int MaxDex = -1, CheckPenalty, SpellFailure;
    public bool HasArmor, Plot, Stolen, Cursed, Usable;
    public List<ItemLine> Properties = new();
    public List<ItemLine> Requirements = new();

    public bool IsMagic => Properties.Count > 0;

    public static ItemInfo? From(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        string S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        int I(string n, int d = 0) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : d;
        bool B(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.True;
        var info = new ItemInfo
        {
            Type = S("type"), Category = S("category"), Damage = S("damage"), DamageType = S("damageType"), Critical = S("critical"),
            InventorySound = e.TryGetProperty("inventorySound", out var sound) && sound.ValueKind == JsonValueKind.String ? sound.GetString() : null,
            Panel = I("panel", 1), AC = I("ac", -1), Charges = I("charges", -1), Weight = I("weight"), Value = I("value", -1),
            MaxStack = I("maxStack", 1), Width = I("width", 1), Height = I("height", 1), Slots = I("slots"),
            Plot = B("plot"), Stolen = B("stolen"), Cursed = B("cursed"), Usable = B("usable"),
        };
        if (e.TryGetProperty("armor", out var a) && a.ValueKind == JsonValueKind.Object)
        {
            info.HasArmor = true;
            info.ArmorWeight = a.TryGetProperty("weight", out var w) ? w.GetString() ?? "" : "";
            info.MaxDex = a.TryGetProperty("maxDex", out var md) ? md.GetInt32() : -1;
            info.CheckPenalty = a.TryGetProperty("checkPenalty", out var cp) ? cp.GetInt32() : 0;
            info.SpellFailure = a.TryGetProperty("spellFailure", out var sf) ? sf.GetInt32() : 0;
        }
        if (e.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Array)
            foreach (var p in props.EnumerateArray())
                info.Properties.Add(new ItemLine(ItemText.Clean(p.GetProperty("text").GetString() ?? ""),
                    p.TryGetProperty("bad", out var bad) && bad.GetBoolean(), p.TryGetProperty("temporary", out var t) && t.GetBoolean()));
        if (e.TryGetProperty("requirements", out var reqs) && reqs.ValueKind == JsonValueKind.Array)
            foreach (var r in reqs.EnumerateArray())
                info.Requirements.Add(new ItemLine(ItemText.Clean(r.GetProperty("text").GetString() ?? ""), Met: !r.TryGetProperty("met", out var m) || m.GetBoolean()));
        return info;
    }
}

/// <summary>An item as the inventory, store and loot windows show it.</summary>
public sealed record ItemEntry(uint Id, string Name, int BaseItem, int Stack, string[] Icon, int Slot = -1, int Price = 0, bool Identified = true, int Panel = 0)
{
    public ItemInfo? Info { get; init; }
    /// <summary>The name as the module wrote it, colour tokens included (for BBCode via <see cref="Kit.NwnText.ToBbcode"/>).</summary>
    public string RawName { get; init; } = "";
    /// <summary>Top-left cell in its inventory grid (-1: equipped or not placed).</summary>
    public int X { get; init; } = -1;
    public int Y { get; init; } = -1;
    /// <summary>The bag (container item) it is in, or 0 when directly in the inventory.</summary>
    public uint Bag { get; init; }
    /// <summary>Cells it covers (baseitems InvSlotWidth x InvSlotHeight).</summary>
    public int W => Math.Max(1, Info?.Width ?? 1);
    public int H => Math.Max(1, Info?.Height ?? 1);
    public bool IsBag => Info?.Category == "container";

    /// <summary>Store panel (0 armour, 1 misc, 2 potions, 3 jewellery, 4 weapons) from the store or the base item.</summary>
    public int StorePanel => Panel;

    public static ItemEntry From(JsonElement e, int slot = -1)
    {
        var info = e.TryGetProperty("info", out var inf) ? ItemInfo.From(inf) : null;
        string raw = e.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        return new ItemEntry(
            e.GetProperty("id").GetUInt32(),
            ItemText.Clean(raw),
            e.TryGetProperty("baseItem", out var b) ? b.GetInt32() : 0,
            e.TryGetProperty("stack", out var s) ? s.GetInt32() : 1,
            e.TryGetProperty("icon", out var i) && i.ValueKind == JsonValueKind.Array ? i.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : [],
            slot,
            e.TryGetProperty("price", out var p) ? p.GetInt32() : 0,
            !e.TryGetProperty("identified", out var id) || id.GetBoolean(),
            e.TryGetProperty("panel", out var pn) ? pn.GetInt32() : info?.Panel ?? 1)
        {
            Info = info, RawName = raw,
            X = e.TryGetProperty("x", out var x) ? x.GetInt32() : -1,
            Y = e.TryGetProperty("y", out var y) ? y.GetInt32() : -1,
        };
    }

    /// <summary>The backpack filter tab this item belongs to.</summary>
    public ItemFilter Filter => Info?.Category switch
    {
        "weapon" or "ammo" => ItemFilter.Weapons,
        "armor" or "shield" or "apparel" or "jewelry" => ItemFilter.Armour,
        "consumable" => ItemFilter.Consumables,
        _ => ItemFilter.Misc,
    };
}

public enum ItemFilter { All, Weapons, Armour, Consumables, Misc }

/// <summary>An inventory grid's shape as the server sends it: columns, rows per page, pages (0 = grows as needed).</summary>
public readonly record struct GridSpec(int Width, int PageHeight, int Pages)
{
    public static readonly GridSpec Backpack = new(10, 6, 6);

    public static GridSpec From(System.Text.Json.JsonElement parent, string name, GridSpec fallback) =>
        parent.TryGetProperty(name, out var g) && g.ValueKind == System.Text.Json.JsonValueKind.Object
            ? new GridSpec(g.GetProperty("width").GetInt32(), g.GetProperty("pageHeight").GetInt32(), g.GetProperty("pages").GetInt32())
            : fallback;

    /// <summary>Inside the grid and on one page.</summary>
    public bool Contains(int x, int y, int w, int h) =>
        x >= 0 && y >= 0 && x + w <= Width && (Pages <= 0 || y + h <= Pages * PageHeight)
        && (h > PageHeight || y / PageHeight == (y + h - 1) / PageHeight);
}

/// <summary>Text helpers for item names coming from the module.</summary>
public static class ItemText
{
    /// <summary>Removes NWN colour tokens (&lt;cRGB&gt;...&lt;/c&gt;) from a name.</summary>
    public static string Clean(string s) => Kit.NwnText.Strip(s);

    /// <summary>"12.5 lb" from tenths of a pound.</summary>
    public static string Weight(int tenths) => tenths % 10 == 0 ? $"{tenths / 10} lb" : $"{tenths / 10.0:0.0} lb";
}
