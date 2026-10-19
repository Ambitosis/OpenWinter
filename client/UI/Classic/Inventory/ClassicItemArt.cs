using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// How the original draws items: icons at texel size with power-of-two padding cropped, stack counts
/// at the bottom-right, the per-cell backgrounds (gui_invm_*: hovered, unidentified, unusable) and the
/// equipment silhouettes (inv_slot_*). Also the drag data classic item windows hand around.
/// </summary>
public static partial class ClassicItemArt
{
    /// <summary>Background of an item's cells: plain, hovered, unidentified (blue), unusable (red).</summary>
    public const string CellPlain = "gui_invm_unhl", CellHover = "gui_invm_hl", CellUnidentified = "gui_invm_hl_xid", CellUnusable = "gui_invm_hl_xuse", CellBlocked = "gui_invm_ocupied";
    /// <summary>Equipment boxes that can (hilite) or can't (unhilite) take the item being carried.</summary>
    public const string SlotHilite = "gui_inv_hilite", SlotUnhilite = "gui_inv_unhilite";

    public static readonly Color CountColor = new(1f, 1f, 1f);

    /// <summary>The silhouette shown in an empty equipment box (INVENTORY_SLOT_*).</summary>
    public static string Silhouette(int slot) => slot switch
    {
        0 => "inv_slot_helm", 1 => "inv_slot_armor", 2 => "inv_slot_boots", 3 => "inv_slot_gloves", 4 => "inv_slot_right",
        5 => "inv_slot_left", 6 => "inv_slot_cloak", 7 or 8 => "inv_slot_ring", 9 => "inv_slot_amulet", 10 => "inv_slot_belt",
        11 => "inv_slot_arrow", 12 => "inv_slot_sling", 13 => "inv_slot_bolts", _ => "",
    };

    /// <summary>The occupied icon canvas; power-of-two texture padding is outside the inventory footprint.</summary>
    public static Vector2 IconSize(ClassicAssets assets, ItemEntry item)
    {
        foreach (var layer in item.Icon)
            if (assets.Texture(layer) is { } t) return t.GetSize().Min(new Vector2(32 * item.W, 32 * item.H));
        return new Vector2(32 * item.W, 32 * item.H);
    }

    /// <summary>Draws the occupied part of each icon layer, excluding unused power-of-two padding.</summary>
    public static void DrawIcon(CanvasItem ci, ClassicAssets assets, ItemEntry item, Rect2 area, Color modulate, Vector2? centre = null)
    {
        var canvas = IconSize(assets, item);
        float scale = Math.Min(1f, Math.Min(area.Size.X / canvas.X, area.Size.Y / canvas.Y));
        var size = canvas * scale;
        var pos = (centre ?? area.GetCenter()) - size / 2;
        pos.X = Math.Clamp(pos.X, area.Position.X, Math.Max(area.Position.X, area.End.X - size.X));
        pos.Y = Math.Clamp(pos.Y, area.Position.Y, Math.Max(area.Position.Y, area.End.Y - size.Y));
        foreach (var layer in item.Icon)
        {
            if (assets.Texture(layer) is not { } tex) continue;
            ci.DrawTextureRectRegion(tex, new Rect2(pos.Round(), size), new Rect2(Vector2.Zero, canvas), modulate);
        }
    }

    /// <summary>A texture at its own size (shrunk to fit <paramref name="area"/>), centred on the area or on <paramref name="centre"/>.</summary>
    public static void DrawFitted(CanvasItem ci, Texture2D tex, Rect2 area, Color modulate, Vector2? centre = null)
    {
        var size = tex.GetSize();
        float scale = Math.Min(1f, Math.Min(area.Size.X / size.X, area.Size.Y / size.Y));
        size *= scale;
        var c = centre ?? area.GetCenter();
        // Keep it inside the area when centred on a node near an edge.
        var pos = c - size / 2;
        pos.X = Math.Clamp(pos.X, area.Position.X, Math.Max(area.Position.X, area.End.X - size.X));
        pos.Y = Math.Clamp(pos.Y, area.Position.Y, Math.Max(area.Position.Y, area.End.Y - size.Y));
        ci.DrawTextureRect(tex, new Rect2(pos.Round(), size), false, modulate);
    }

    /// <summary>The count shown on an item: its stack size, none for gold (BASE_ITEM_GOLD, as the original).</summary>
    public static int CountOf(ItemEntry item) => item.BaseItem == 76 ? 1 : item.Stack;

    /// <summary>The stack size at the bottom-right of an item's area.</summary>
    public static void DrawCount(CanvasItem ci, ClassicAssets assets, int stack, Rect2 area)
    {
        if (stack <= 1) return;
        var font = assets.Font(GuiFont.DefaultName);
        var text = stack.ToString();
        float w = font.Measure(text);
        var at = new Vector2(area.End.X - w - 1, area.End.Y - font.LineHeight);
        // A dark edge so the count reads over bright icons.
        foreach (var o in new[] { new Vector2(1, 1), new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, 1) })
            font.Draw(ci, at + o, text, new Color(0, 0, 0, 0.85f));
        font.Draw(ci, at, text, CountColor);
    }

    /// <summary>Tiles a 32 px cell texture over an item's cells.</summary>
    public static void DrawCells(CanvasItem ci, Texture2D? tex, Rect2 area, Vector2 cell)
    {
        if (tex == null) return;
        int cols = Math.Max(1, (int)MathF.Round(area.Size.X / cell.X)), rows = Math.Max(1, (int)MathF.Round(area.Size.Y / cell.Y));
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                ci.DrawTextureRect(tex, new Rect2(area.Position + new Vector2(x * cell.X, y * cell.Y), cell), false);
    }

    /// <summary>
    /// Drag data for an item from a classic window: the action bar's "item" dictionary (docs/UI.md) carrying the
    /// <see cref="ItemDrag"/> under "drag", so the quickbar and every item window read it.
    /// </summary>
    public static Godot.Collections.Dictionary DragData(ItemDrag drag) => new()
    {
        ["kind"] = "item", ["item"] = (long)drag.Item.Id, ["name"] = drag.Item.Name, ["drag"] = drag,
    };

    /// <summary>The cursor picture while carrying an item: its icon at texel size, held where it was grabbed.</summary>
    public static Control DragPreview(ClassicAssets assets, ItemEntry item, Vector2 grabOffset)
    {
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        var icon = new ItemPicture { Assets = assets, Item = item, MouseFilter = Control.MouseFilterEnum.Ignore };
        icon.Size = IconSize(assets, item);
        icon.Position = -grabOffset;
        holder.AddChild(icon);
        return holder;
    }

    /// <summary>An item's icon (all layers) with its stack count, sized by its owner.</summary>
    public partial class ItemPicture : Control
    {
        public ClassicAssets Assets = null!;
        public ItemEntry? Item;
        public override void _Draw()
        {
            if (Item == null) return;
            var r = new Rect2(Vector2.Zero, Size);
            DrawIcon(this, Assets, Item, r, Colors.White);
            DrawCount(this, Assets, CountOf(Item), r);
        }
    }

    /// <summary>Whether the controlled character can't use the item (requirements not met): drawn on red.</summary>
    public static bool Unusable(ItemEntry item) => item.Info?.Requirements.Any(r => !r.Met) == true;

    /// <summary>The colour the original gives an item's name: plain white, blue for unidentified magic.</summary>
    public static Color NameColor(ItemEntry item) => item.Identified ? Colors.White : new Color(0.6f, 0.7f, 1f);
}
