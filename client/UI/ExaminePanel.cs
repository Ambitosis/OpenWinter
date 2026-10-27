using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The examine window: a card for any object. Items show their icon, coloured name, type, stat chips
/// (damage, armour class, weight, value), properties and requirements; creatures their portrait with
/// race and classes; the description is set in the reading face.
/// </summary>
public partial class ExaminePanel : UiTheme.Window
{
    public ClientSession Session = null!;
    VBoxContainer _content = null!;
    ScrollContainer _scroll = null!;
    bool _placed;

    public static ExaminePanel Create(ClientSession session)
    {
        var p = Create<ExaminePanel>("Examine", new Vector2(520, 0));
        p.Session = session;
        return p;
    }

    public override void _Ready()
    {
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        Body.AddChild(_scroll);
        _content = Ui.VBox(14);
        _content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var pad = Ui.Pad(_content, 0, 0, 10, 0);
        pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(pad);
        VisibilityChanged += () =>
        {
            if (!Visible || _placed) return;
            _placed = true;
            var view = GetViewportRect().Size;
            Position = new Vector2(Math.Max(16, (view.X - Size.X) / 2), Math.Max(16, (view.Y - Size.Y) / 2 - 30));
        };
        Session.NotifyReceived += OnNotify;
    }

    /// <summary>Asks the server to describe an object.</summary>
    public void Examine(uint id)
    {
        // A disabled examine panel stays shut and the attempt goes to the module (SetGuiPanelDisabled).
        if (GuiPanels.Current?.AllowExamine(id) == false) return;
        Session.Request("examine", new { @object = id });
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != "examine") return;
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        foreach (var c in _content.GetChildren()) { _content.RemoveChild(c); c.QueueFree(); }
        var content = Session.Content;
        string rawName = d.GetProperty("name").GetString() ?? "";
        string kind = d.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
        var icon = d.GetProperty("icon").EnumerateArray().Select(i => i.GetString() ?? "").ToArray();
        string portrait = d.GetProperty("portrait").GetString() ?? "";
        string subtitle = d.TryGetProperty("subtitle", out var st) ? st.GetString() ?? "" : "";
        var info = d.TryGetProperty("info", out var inf) ? ItemInfo.From(inf) : null;
        bool identified = !d.TryGetProperty("identified", out var idf) || idf.GetBoolean();
        var item = kind == "item"
            ? new ItemEntry(d.GetProperty("object").GetUInt32(), NwnText.Strip(rawName), 0, 1, icon, Identified: identified) { Info = info, RawName = rawName }
            : null;
        TitleLabel.Text = kind switch { "item" => "Item", "creature" => "Creature", "placeable" => "Object", "door" => "Door", "trap" => "Trap", _ => "Examine" };

        // ---- header: picture and name
        var head = Ui.HBox(16);
        _content.AddChild(head);
        if (item != null && content != null)
        {
            var frame = new PanelContainer { CustomMinimumSize = new Vector2(84, 84) };
            var fs = Styles.Slot();
            fs.SetContentMarginAll(6);
            if (ItemUi.RarityTint(item) is { } tint) fs.BorderColor = tint with { A = 0.7f };
            frame.AddThemeStyleboxOverride("panel", fs);
            frame.AddChild(UiTheme.Icon(content, icon, 1, 72));
            head.AddChild(frame);
        }
        else if (kind == "trap" && content != null && icon.Length > 0)
            head.AddChild(UiTheme.Icon(content, icon, 1, 72)); // Examine Trap: the trap's icon (traps.2da IconResRef)
        else if (content != null && Portrait(content, portrait) is { } pic) head.AddChild(pic);

        var titles = Ui.VBox(4);
        titles.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titles.Alignment = BoxContainer.AlignmentMode.Center;
        head.AddChild(titles);
        var name = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(item != null ? 360 : 340, 0),
            Text = NwnText.ToBbcode(rawName, Palette.Hex(item != null ? ItemUi.NameColor(item) : Palette.AccentBright)),
        };
        name.AddThemeFontOverride("normal_font", Fonts.Display);
        name.AddThemeFontSizeOverride("normal_font_size", 22);
        titles.AddChild(name);
        string sub = item != null ? info?.Type ?? "" : subtitle;
        if (item != null && info?.HasArmor == true && info.ArmorWeight is { Length: > 0 } aw && aw != "Shield") sub = $"{aw} {sub.ToLowerInvariant()}";
        if (sub.Length > 0) titles.AddChild(Ui.Text(sub, Ui.TextStyle.Secondary));
        if (item != null && (info?.Plot == true || !identified || info?.Stolen == true || info?.Cursed == true))
        {
            var tags = Ui.HBox(6);
            if (info?.Plot == true) tags.AddChild(ItemTooltip.Tag("PLOT", Palette.Plot));
            if (!identified) tags.AddChild(ItemTooltip.Tag("UNIDENTIFIED", Palette.TextSecondary));
            if (info?.Stolen == true) tags.AddChild(ItemTooltip.Tag("STOLEN", Palette.Warning));
            if (info?.Cursed == true) tags.AddChild(ItemTooltip.Tag("CURSED", Palette.Danger));
            titles.AddChild(tags);
        }

        // ---- item figures, properties, requirements
        if (item != null && info != null)
        {
            var chips = new HFlowContainer();
            chips.AddThemeConstantOverride("h_separation", 8);
            chips.AddThemeConstantOverride("v_separation", 8);
            if (info.Damage.Length > 0) chips.AddChild(Chip("Damage", info.Damage, info.DamageType));
            if (info.Critical.Length > 0) chips.AddChild(Chip("Critical", info.Critical));
            if (info.AC >= 0) chips.AddChild(Chip("Armour class", info.AC.ToString()));
            if (info.HasArmor && info.MaxDex >= 0) chips.AddChild(Chip("Max Dex", "+" + info.MaxDex));
            if (info.HasArmor && info.SpellFailure > 0) chips.AddChild(Chip("Spell failure", info.SpellFailure + "%"));
            if (info.Charges >= 0) chips.AddChild(Chip("Charges", info.Charges.ToString()));
            chips.AddChild(Chip("Weight", ItemText.Weight(info.Weight)));
            chips.AddChild(Chip("Value", info.Value < 0 ? "Unknown" : ItemUi.Gp(info.Value), color: Palette.AccentBright));
            _content.AddChild(chips);

            if (!identified)
            {
                var l = Ui.Text("Its properties are unknown until it is identified: a merchant can, or a character skilled in Lore.", Ui.TextStyle.ReadingItalic, Palette.TextSecondary, 17);
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _content.AddChild(l);
            }
            else if (info.Properties.Count > 0)
            {
                _content.AddChild(Ui.Section("Properties"));
                var list = Ui.VBox(4);
                foreach (var p in info.Properties)
                {
                    var l = Ui.Text("◆  " + p.Text + (p.Temporary ? "  (temporary)" : ""), Ui.TextStyle.Body, p.Bad ? Palette.Danger : Palette.Magic);
                    l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    list.AddChild(l);
                }
                _content.AddChild(list);
            }
            if (info.Requirements.Count > 0)
            {
                _content.AddChild(Ui.Section("Requires"));
                foreach (var r in info.Requirements)
                {
                    var l = Ui.Text(r.Text + (r.Met ? "" : "  (you don't meet this)"), Ui.TextStyle.Body, r.Met ? Palette.TextSecondary : Palette.Danger);
                    l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    _content.AddChild(l);
                }
            }
        }
        else if (d.TryGetProperty("properties", out var props))
            foreach (var p in props.EnumerateArray())
                _content.AddChild(Ui.Text(NwnText.Strip(p.GetString() ?? ""), Ui.TextStyle.Body, Palette.Magic));

        // ---- description
        string description = d.GetProperty("description").GetString() ?? "";
        if (description.Trim().Length > 0)
        {
            _content.AddChild(Ui.Section("Description"));
            var text = new RichTextLabel
            {
                CustomMinimumSize = new Vector2(470, 0),
                BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, SelectionEnabled = true,
                Text = NwnText.ToBbcode(description.Trim()),
            };
            text.AddThemeFontOverride("normal_font", Fonts.Reading);
            text.AddThemeFontSizeOverride("normal_font_size", 18);
            text.AddThemeColorOverride("default_color", Palette.Text with { A = 0.92f });
            text.AddThemeConstantOverride("line_separation", 3);
            _content.AddChild(text);
        }
        _scroll.ScrollVertical = 0;
        if (Visible) WindowStack.Activate(this);
        Visible = true;
        // Fit the card to what it shows, up to a reading height; longer text scrolls.
        Callable.From(() =>
        {
            if (!IsInsideTree()) return;
            float want = _content.GetCombinedMinimumSize().Y + 4;
            _scroll.CustomMinimumSize = new Vector2(0, Math.Clamp(want, 140, Math.Min(560, GetViewportRect().Size.Y - 200)));
            ResetSize();
        }).CallDeferred();
    }

    /// <summary>A labelled figure: "DAMAGE / 1d8 +1 / Slashing".</summary>
    static Control Chip(string label, string value, string? detail = null, Color? color = null)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Styles.Box(new Color(1, 1, 1, 0.035f), Palette.RadiusSmall, Palette.Line, 1, 12, 7));
        var v = Ui.VBox(1);
        var cap = Ui.Text(label.ToUpperInvariant(), Ui.TextStyle.Caption, Palette.TextMuted, 10);
        cap.AddThemeFontOverride("font", Fonts.UiBold);
        v.AddChild(cap);
        var val = Ui.Text(value, Ui.TextStyle.Strong, color ?? Palette.Text, Palette.FontLarge);
        v.AddChild(val);
        if (detail is { Length: > 0 }) v.AddChild(Ui.Text(detail, Ui.TextStyle.Caption, Palette.TextSecondary));
        p.AddChild(v);
        return p;
    }

    /// <summary>A creature's portrait (the largest size the module has), cropped to the painted area, in a frame.</summary>
    static Control? Portrait(Views.ClientContent content, string portrait)
    {
        if (portrait.Length == 0) return null;
        var tex = content.Textures.Get(portrait + "h") ?? content.Textures.Get(portrait + "l") ?? content.Textures.Get(portrait + "m");
        if (tex == null) return null;
        float w = tex.Texture.GetWidth(), h = tex.Texture.GetHeight();
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(96, 150) };
        var fs = Styles.Slot();
        fs.SetContentMarginAll(3);
        fs.BorderColor = Palette.AccentDim;
        frame.AddThemeStyleboxOverride("panel", fs);
        frame.AddChild(new TextureRect
        {
            Texture = new AtlasTexture { Atlas = tex.Texture, Region = new Rect2(0, tex.FlipV ? h * 28f / 128f : 0, w, h * 100f / 128f) },
            FlipV = tex.FlipV, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        });
        return frame;
    }
}
