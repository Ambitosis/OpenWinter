using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>Item presentation shared by every item window: name colours, prices, the rich tooltip.</summary>
public static class ItemUi
{
    /// <summary>Plot items purple, identified magic items blue, unidentified muted, the rest plain.</summary>
    public static Color NameColor(ItemEntry item)
    {
        if (item.Info?.Plot == true) return Palette.Plot;
        if (!item.Identified) return Palette.TextMuted.Lightened(0.15f);
        if (IsMagic(item)) return Palette.Magic;
        return Palette.Text;
    }

    /// <summary>Identified items with properties, except consumables (every potion "casts a spell").</summary>
    public static bool IsMagic(ItemEntry item) =>
        item.Identified && item.Info is { } i && i.IsMagic && i.Category != "consumable";

    /// <summary>Slot border tint for an item's rarity (null: the plain slot).</summary>
    public static Color? RarityTint(ItemEntry? item)
    {
        if (item == null) return null;
        if (item.Info?.Plot == true) return Palette.Plot;
        if (IsMagic(item)) return Palette.Magic;
        return null;
    }

    public static string Gp(int amount) => amount.ToString("N0") + " gp";

    /// <summary>
    /// The item's name as rich text: the module's own colour tokens where it has them, otherwise
    /// the rarity colour. Wraps at <paramref name="width"/> (0: single line, clipped).
    /// </summary>
    public static RichTextLabel NameLabel(ItemEntry item, int size, float width = 0, Font? font = null)
    {
        var l = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = width > 0 ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            CustomMinimumSize = new Vector2(width, 0),
            Text = NwnText.ToBbcode(item.RawName.Length > 0 ? item.RawName : item.Name, Palette.Hex(NameColor(item))),
        };
        l.AddThemeFontOverride("normal_font", font ?? Fonts.UiBold);
        l.AddThemeFontSizeOverride("normal_font_size", size);
        if (width <= 0) l.ClipContents = true;
        return l;
    }

    /// <summary>A rounded toggle chip with an icon and optional text (filter tabs).</summary>
    public static Button Chip(string icon, string text, string tooltip, bool pressed, Action<Button> chosen)
    {
        var b = new Button
        {
            ToggleMode = true, ButtonPressed = pressed, FocusMode = Control.FocusModeEnum.None, TooltipText = tooltip,
            Icon = Icons.Get(icon, 36), ExpandIcon = false, CustomMinimumSize = new Vector2(text.Length > 0 ? 0 : 44, 32),
            Text = text, IconAlignment = text.Length > 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center,
        };
        b.AddThemeConstantOverride("icon_max_width", 17);
        b.AddThemeFontSizeOverride("font_size", Palette.FontSmall);
        b.AddThemeStyleboxOverride("normal", Styles.Box(new Color(1, 1, 1, 0.03f), 16, Palette.Line, 1, 11, 4));
        b.AddThemeStyleboxOverride("hover", Styles.Box(new Color(1, 1, 1, 0.07f), 16, Palette.LineStrong, 1, 11, 4));
        var on = Styles.Box(Palette.Accent with { A = 0.16f }, 16, Palette.Accent, 1, 11, 4);
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        b.AddThemeColorOverride("icon_normal_color", Palette.TextSecondary);
        b.AddThemeColorOverride("icon_hover_color", Palette.Text);
        b.AddThemeColorOverride("icon_pressed_color", Palette.AccentBright);
        b.AddThemeColorOverride("icon_hover_pressed_color", Palette.AccentBright);
        b.AddThemeColorOverride("font_pressed_color", Palette.AccentBright);
        b.Pressed += () => chosen(b);
        return b;
    }

    /// <summary>A gold amount with the coin glyph.</summary>
    public static HBoxContainer Price(int amount, Color? color = null, int size = Palette.FontSmall + 1, string? prefix = null)
    {
        var row = Ui.HBox(4);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (prefix != null) row.AddChild(Ui.Text(prefix, Ui.TextStyle.Small));
        row.AddChild(Icons.Rect("gold", size + 1, Palette.Accent));
        var l = Ui.Text(amount < 0 ? "—" : amount.ToString("N0"), Ui.TextStyle.Strong, color ?? Palette.AccentBright, size);
        row.AddChild(l);
        return row;
    }

    /// <summary>Interface glyph for a backpack filter.</summary>
    public static string FilterIcon(ItemFilter f) => f switch
    {
        ItemFilter.Weapons => "weapon",
        ItemFilter.Armour => "armor",
        ItemFilter.Consumables => "potion",
        ItemFilter.Misc => "chest",
        _ => "inventory",
    };

    public static string FilterName(ItemFilter f) => f switch
    {
        ItemFilter.Weapons => "Weapons",
        ItemFilter.Armour => "Armour & apparel",
        ItemFilter.Consumables => "Consumables",
        ItemFilter.Misc => "Miscellaneous",
        _ => "All items",
    };
}

/// <summary>
/// The rich item tooltip: icon and coloured name, base type, damage or armour class, properties,
/// charges, requirements (unmet in red), flags, weight and value, and an optional trade line and hint.
/// Returned from <see cref="Control._MakeCustomTooltip"/> by <see cref="ItemSlot"/> and item rows.
/// </summary>
public static class ItemTooltip
{
    /// <summary>Context lines a window adds: a price ("Buy for 1,800 gp"), a note, and a usage hint.</summary>
    public sealed class Extra
    {
        public string? PriceLabel;
        public int Price = -1;
        public bool PriceBad;
        public string? Note;
        public Color? NoteColor;
        public string? Hint;
    }

    const int Width = 310;

    public static Control Build(ItemEntry item, ClientContent? content, Extra? extra = null) => TooltipCard.Wrap(Content(item, content, extra));

    /// <summary>The tooltip's contents without the card (to embed elsewhere).</summary>
    public static Control Content(ItemEntry item, ClientContent? content, Extra? extra = null)
    {
        var info = item.Info;
        var root = Ui.VBox(8);
        root.CustomMinimumSize = new Vector2(Width, 0);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;

        // ---- header: icon, name, type
        var head = Ui.HBox(12);
        root.AddChild(head);
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(54, 54), MouseFilter = Control.MouseFilterEnum.Ignore };
        var fs = Styles.Slot();
        if (ItemUi.RarityTint(item) is { } tint) fs.BorderColor = tint with { A = 0.7f };
        frame.AddThemeStyleboxOverride("panel", fs);
        if (content != null) frame.AddChild(UiTheme.Icon(content, item.Icon, 1, 46));
        head.AddChild(frame);
        var titles = Ui.VBox(2);
        titles.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titles.Alignment = BoxContainer.AlignmentMode.Center;
        head.AddChild(titles);
        titles.AddChild(ItemUi.NameLabel(item, Palette.FontLarge, Width - 66));
        var sub = new List<string>();
        if (info?.Type is { Length: > 0 } type) sub.Add(info.HasArmor && info.ArmorWeight is { Length: > 0 } aw && aw != "Shield" ? $"{aw} {type.ToLowerInvariant()}" : type);
        if (item.Stack > 1) sub.Add(info?.MaxStack > 1 ? $"Stack {item.Stack} / {info.MaxStack}" : $"Stack {item.Stack}");
        if (sub.Count > 0) titles.AddChild(Ui.Text(string.Join("  ·  ", sub), Ui.TextStyle.Small));
        if (info?.Plot == true || !item.Identified)
        {
            var tags = Ui.HBox(6);
            if (info?.Plot == true) tags.AddChild(Tag("PLOT", Palette.Plot));
            if (!item.Identified) tags.AddChild(Tag("UNIDENTIFIED", Palette.TextSecondary));
            titles.AddChild(tags);
        }

        if (info == null)
        {
            AddFooter(root, item, null, extra);
            return root;
        }

        // ---- primary figure: weapon damage or armour class
        if (info.Damage.Length > 0)
        {
            root.AddChild(Rule());
            var row = Ui.HBox(12);
            var big = Ui.Text(info.Damage, Ui.TextStyle.Number, Palette.Text, 24);
            row.AddChild(big);
            var col = Ui.VBox(0);
            col.Alignment = BoxContainer.AlignmentMode.Center;
            col.AddChild(Ui.Text(info.DamageType.Length > 0 ? info.DamageType + " damage" : "Damage", Ui.TextStyle.Small, Palette.Text));
            if (info.Critical.Length > 0) col.AddChild(Ui.Text("Critical " + info.Critical, Ui.TextStyle.Caption, Palette.TextSecondary));
            row.AddChild(col);
            root.AddChild(row);
        }
        else if (info.AC >= 0)
        {
            root.AddChild(Rule());
            var row = Ui.HBox(12);
            var big = Ui.Text(info.AC.ToString(), Ui.TextStyle.Number, Palette.Text, 26);
            row.AddChild(big);
            var col = Ui.VBox(0);
            col.Alignment = BoxContainer.AlignmentMode.Center;
            col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            col.AddChild(Ui.Text("Armour Class", Ui.TextStyle.Small, Palette.Text));
            if (info.HasArmor)
            {
                var bits = new List<string>();
                if (info.MaxDex >= 0) bits.Add($"Max Dex +{info.MaxDex}");
                if (info.CheckPenalty != 0) bits.Add($"Check {info.CheckPenalty:+0;−0}");
                if (info.SpellFailure > 0) bits.Add($"Spell failure {info.SpellFailure}%");
                if (bits.Count > 0)
                {
                    var l = Ui.Text(string.Join("  ·  ", bits), Ui.TextStyle.Caption, Palette.TextSecondary);
                    l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    l.CustomMinimumSize = new Vector2(Width - 70, 0);
                    col.AddChild(l);
                }
            }
            row.AddChild(col);
            root.AddChild(row);
        }

        // ---- properties
        if (!item.Identified)
        {
            root.AddChild(Rule());
            var l = Ui.Text("Its properties are unknown until it is identified.", Ui.TextStyle.ReadingItalic, Palette.TextSecondary, 16);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(Width, 0);
            root.AddChild(l);
        }
        else if (info.Properties.Count > 0)
        {
            root.AddChild(Rule());
            var list = Ui.VBox(3);
            foreach (var p in info.Properties)
            {
                var color = p.Bad ? Palette.Danger : ItemUi.IsMagic(item) || info.Category == "consumable" ? Palette.Magic : Palette.Text;
                list.AddChild(Bullet(p.Text + (p.Temporary ? "  (temporary)" : ""), color));
            }
            root.AddChild(list);
        }
        if (info.Charges >= 0) root.AddChild(Ui.Stat("Charges", info.Charges.ToString(), info.Charges == 0 ? Palette.Danger : Palette.Text));

        // ---- requirements and flags
        if (info.Requirements.Count > 0)
        {
            var reqs = Ui.VBox(2);
            var caption = Ui.Text("REQUIRES", Ui.TextStyle.Caption, Palette.TextMuted);
            caption.AddThemeFontOverride("font", Fonts.UiBold);
            reqs.AddChild(caption);
            foreach (var r in info.Requirements)
            {
                var l = Ui.Text(r.Text, Ui.TextStyle.Small, r.Met ? Palette.TextSecondary : Palette.Danger);
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                l.CustomMinimumSize = new Vector2(Width, 0);
                reqs.AddChild(l);
            }
            root.AddChild(reqs);
        }
        if (info.Stolen) root.AddChild(Ui.Text("Stolen", Ui.TextStyle.Small, Palette.Warning));
        if (info.Cursed) root.AddChild(Ui.Text("Cursed: it cannot be dropped or sold", Ui.TextStyle.Small, Palette.Danger));

        AddFooter(root, item, info, extra);
        return root;
    }

    static void AddFooter(VBoxContainer root, ItemEntry item, ItemInfo? info, Extra? extra)
    {
        root.AddChild(Rule());
        var foot = Ui.HBox(8);
        if (info != null) foot.AddChild(Ui.Text(ItemText.Weight(info.Weight), Ui.TextStyle.Small));
        foot.AddChild(Ui.Spacer());
        foot.AddChild(ItemUi.Price(info?.Value ?? -1, Palette.AccentBright));
        root.AddChild(foot);
        if (extra == null) return;
        if (extra.PriceLabel != null)
        {
            var trade = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            trade.AddThemeStyleboxOverride("panel", Styles.Box(new Color(1, 1, 1, 0.04f), Palette.RadiusSmall, Palette.Line, 1, 10, 6));
            var row = Ui.HBox(8);
            row.AddChild(Ui.Text(extra.PriceLabel, Ui.TextStyle.Small, Palette.Text));
            row.AddChild(Ui.Spacer());
            if (extra.Price >= 0) row.AddChild(ItemUi.Price(extra.Price, extra.PriceBad ? Palette.Danger : Palette.AccentBright));
            trade.AddChild(row);
            root.AddChild(trade);
        }
        if (extra.Note != null)
        {
            var l = Ui.Text(extra.Note, Ui.TextStyle.Small, extra.NoteColor ?? Palette.Warning);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(Width, 0);
            root.AddChild(l);
        }
        if (extra.Hint != null)
        {
            var l = Ui.Text(extra.Hint, Ui.TextStyle.Caption, Palette.TextMuted);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(Width, 0);
            root.AddChild(l);
        }
    }

    static Control Rule() => new Divider { Color = Palette.LineStrong, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(0, 1) };

    static Control Bullet(string text, Color color)
    {
        var row = Ui.HBox(7);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        var dot = Ui.Text("◆", Ui.TextStyle.Caption, color with { A = 0.7f }, 8);
        dot.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        dot.CustomMinimumSize = new Vector2(8, 18);
        dot.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(dot);
        var l = Ui.Text(text, Ui.TextStyle.Small, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(Width - 15, 0);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(l);
        return row;
    }

    /// <summary>A small outlined label ("PLOT", "UNIDENTIFIED").</summary>
    public static Control Tag(string text, Color color)
    {
        var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        p.AddThemeStyleboxOverride("panel", Styles.Box(color with { A = 0.12f }, 4, color with { A = 0.45f }, 1, 6, 1));
        var l = Ui.Text(text, Ui.TextStyle.Caption, color, 10);
        l.AddThemeFontOverride("font", Fonts.UiBold);
        p.AddChild(l);
        return p;
    }
}

/// <summary>
/// A custom tooltip's own card (the kit's tooltip surface). It replaces the popup's panel, which
/// would otherwise draw a second, translucent frame around it.
/// </summary>
public partial class TooltipCard : PanelContainer
{
    public static TooltipCard Wrap(Control content)
    {
        var card = new TooltipCard { MouseFilter = MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", Styles.Tooltip());
        card.AddChild(content);
        return card;
    }

    public override void _EnterTree()
    {
        if (GetParent() is Window popup)
        {
            popup.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            popup.TransparentBg = true;
        }
    }
}
